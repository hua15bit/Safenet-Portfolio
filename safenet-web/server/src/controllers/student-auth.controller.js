const crypto = require('node:crypto');
// Handles class-code checks and student registration, login, profile, and logout.
const { z } = require('zod');

const Class = require('../models/class.model');
const { emailVerificationMode } = require('../config/email-verification');
const Student = require('../models/student.model');
const StudentSession = require('../models/student-session.model');
const studentPayload = require('../utils/student-payload');
const {
  consumeRegistrationOtp,
  requestRegistrationLink,
  requestRegistrationOtp,
  verifyRegistrationLinkSession,
  verifyRegistrationOtp,
} = require('../services/email-verification.service');
const {
  consumePasswordResetToken,
  requestPasswordReset,
} = require('../services/password-reset.service');

const studentNumberPattern = /^[A-Za-z0-9-]{5,20}$/;
const classCodePattern = /^[A-HJ-NP-Z2-9]{6}$/;

const registrationSchema = z.strictObject({
  website: z.string().max(0, 'Unable to register.').optional(),
  fullname: z.string().trim().min(3).max(120),
  student_number: z.string().trim().regex(studentNumberPattern),
  program: z.string().trim().min(1).max(120),
  email: z.string().trim().email().max(190),
  password: z.string().min(8).max(128).refine((value) => Buffer.byteLength(value, 'utf8') <= 72, 'Password must be at most 72 UTF-8 bytes.'),
  otp: z.string().trim().regex(/^\d{6}$/, 'Enter the six-digit verification code.').optional(),
  verification_id: z.string().trim().regex(/^[a-fA-F0-9]{24}$/).optional(),
  verification_proof: z.string().trim().regex(/^[A-Za-z0-9_-]{43}$/).optional(),
  // Only an omitted or blank code skips enrollment; a supplied code must be valid.
  class_code: z.string().trim().toUpperCase()
    .refine((code) => code === '' || classCodePattern.test(code), 'Enter a valid six-character class code.')
    .optional(),
});

const otpRequestSchema = z.strictObject({
  email: z.string().trim().email().max(190),
});

const forgotPasswordSchema = z.strictObject({
  email: z.string().trim().email().max(190),
});

const resetPasswordSchema = z.strictObject({
  token: z.string().trim().min(1).max(200),
  password: z.string().min(8).max(128).refine((value) => Buffer.byteLength(value, 'utf8') <= 72, 'Password must be at most 72 UTF-8 bytes.'),
});

const loginSchema = z
  .strictObject({
    identifier: z.string().trim().max(190).optional(),
    email: z.string().trim().max(190).optional(),
    password: z.string().min(1).max(128),
    class_code: z.string().trim().toUpperCase().regex(classCodePattern),
  })
  .refine((data) => Boolean(data.identifier || data.email), {
    path: ['identifier'],
    message: 'Email or student number is required.',
  });

function validationFailure(response, result) {
  return response.status(422).json({
    success: false,
    message: 'Please check all submitted information.',
    errors: result.error.issues.map((issue) => ({
      field: issue.path.join('.'),
      message: issue.message,
    })),
  });
}

async function verifyClassCode(request, response, next) {
  // Confirm that the professor-issued code belongs to an active class.
  try {
    const code = String(request.query.code ?? '').trim().toUpperCase();
    if (!classCodePattern.test(code)) {
      return response.status(422).json({
        success: false,
        message: 'Enter a valid six-character class code.',
      });
    }

    const classDocument = await Class.findOne({
      classCode: code,
      isActive: true,
    }).populate('professor', 'fullname');

    if (!classDocument) {
      return response.status(404).json({
        success: false,
        message: 'No active class matches this code.',
      });
    }

    return response.json({
      success: true,
      class: {
        id: classDocument._id.toString(),
        subject: classDocument.subject,
        section: classDocument.section,
        schedule: classDocument.schedule,
        term: classDocument.term,
        professor: classDocument.professor.fullname,
        code,
      },
    });
  } catch (error) {
    return next(error);
  }
}

async function registerStudent(request, response, next) {
  // Create either an enrolled account or an account awaiting professor enrollment.
  try {
    const result = registrationSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const data = result.data;
    const classDocument = data.class_code ? await Class.findOne({
      classCode: data.class_code,
      isActive: true,
    }) : null;
    if (data.class_code && !classDocument) {
      return response.status(404).json({
        success: false,
        message: 'The class code is invalid or inactive.',
      });
    }

    const duplicate = await Student.exists({
      $or: [
        { studentNumber: data.student_number.toUpperCase() },
        { email: data.email.toLowerCase() },
      ],
    });
    if (duplicate) {
      return response.status(409).json({
        success: false,
        message: 'That student number or email is already registered.',
      });
    }

    const verificationMode = emailVerificationMode();
    if (verificationMode === 'link') {
      if (!data.verification_id || !data.verification_proof) {
        return response.status(422).json({
          success: false,
          message: 'Send and open the email verification link before creating your account.',
        });
      }
      await verifyRegistrationLinkSession({
        email: data.email,
        purpose: 'student-registration',
        verificationId: data.verification_id,
        verificationProof: data.verification_proof,
      });
    } else if (verificationMode === 'code') {
      if (!data.otp) {
        return response.status(422).json({
          success: false,
          message: 'Enter the six-digit verification code.',
        });
      }
      await verifyRegistrationOtp({
        email: data.email,
        purpose: 'student-registration',
        code: data.otp,
      });
    }

    const student = await Student.create({
      class: classDocument?._id ?? null,
      status: classDocument ? 'enrolled' : 'unenrolled',
      fullname: data.fullname,
      studentNumber: data.student_number.toUpperCase(),
      program: data.program,
      email: data.email.toLowerCase(),
      password: data.password,
    });
    if (verificationMode !== 'disabled') {
      await consumeRegistrationOtp({
        email: data.email,
        purpose: 'student-registration',
      });
    }

    return response.status(201).json({
      success: true,
      message: classDocument ? 'Student registration completed.' : 'Account created. Ask your professor to enroll you before signing in to the game.',
      student: {
        id: student._id.toString(),
        fullname: student.fullname,
        student_number: student.studentNumber,
        email: student.email,
        program: student.program,
        status: student.status,
      },
      class: classDocument ? {
        id: classDocument._id.toString(),
        subject: classDocument.subject,
        section: classDocument.section,
        code: classDocument.classCode,
      } : null,
    });
  } catch (error) {
    if (error?.code === 11000) {
      return response.status(409).json({
        success: false,
        message: 'That student number or email is already registered.',
      });
    }
    return next(error);
  }
}

async function requestStudentRegistrationOtp(request, response, next) {
  // Email a short-lived code before any student account is created.
  try {
    const verificationMode = emailVerificationMode();
    if (verificationMode === 'disabled') {
      return response.status(503).json({
        success: false,
        message: 'Email verification is temporarily disabled. Continue registration without a code.',
      });
    }
    const result = otpRequestSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const email = result.data.email.toLowerCase();
    if (await Student.exists({ email })) {
      return response.status(409).json({
        success: false,
        message: 'That email is already registered.',
      });
    }

    if (verificationMode === 'link') {
      const session = await requestRegistrationLink({
        email,
        purpose: 'student-registration',
        accountType: 'student',
      });
      return response.json({
        success: true,
        message: 'A verification link was sent to your email. Open it, then return here.',
        ...session,
      });
    }

    await requestRegistrationOtp({
      email,
      purpose: 'student-registration',
      accountType: 'student',
    });
    return response.json({
      success: true,
      message: 'A six-digit verification code was sent to your email.',
      expiresInSeconds: 600,
    });
  } catch (error) {
    return next(error);
  }
}

async function loginStudent(request, response, next) {
  // Check credentials and create a revocable session token for the student.
  try {
    const result = loginSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const identifier = result.data.identifier || result.data.email;
    const student = await Student.findOne({
      $or: [
        { email: identifier.toLowerCase() },
        { studentNumber: identifier.toUpperCase() },
      ],
      status: 'enrolled',
    })
      .select('+password')
      .populate({
        path: 'class',
        match: { classCode: result.data.class_code, isActive: true },
      });

    if (!student?.class || !(await student.comparePassword(result.data.password))) {
      return response.status(401).json({
        success: false,
        message: 'Invalid student credentials or inactive account.',
      });
    }

    const rawToken = crypto.randomBytes(32).toString('hex');
    const tokenHash = crypto.createHash('sha256').update(rawToken).digest('hex');
    const expiresAt = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000);

    await StudentSession.deleteMany({
      $or: [
        { expiresAt: { $lte: new Date() } },
        {
          student: student._id,
          createdAt: { $lt: new Date(Date.now() - 24 * 60 * 60 * 1000) },
        },
      ],
    });
    await StudentSession.create({
      student: student._id,
      tokenHash,
      expiresAt,
    });

    return response.json({
      success: true,
      message: 'Login successful.',
      token: rawToken,
      expires_at: expiresAt.toISOString(),
      student: studentPayload(student, student.class),
    });
  } catch (error) {
    return next(error);
  }
}

function currentStudent(request, response) {
  response.json({
    success: true,
    student: studentPayload(request.student, request.studentClass),
  });
}

async function logoutStudent(request, response, next) {
  try {
    await StudentSession.deleteOne({ _id: request.studentSession._id });
    response.json({
      success: true,
      message: 'Logout successful.',
    });
  } catch (error) {
    next(error);
  }
}

// Always answers the same way whether or not the email belongs to an account
// — see password-reset.service.js for why that matters here specifically.
async function forgotStudentPassword(request, response, next) {
  try {
    const result = forgotPasswordSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const email = result.data.email.toLowerCase();
    const account = await Student.exists({ email });
    await requestPasswordReset({ email, accountType: 'student', account });

    return response.json({
      success: true,
      message: 'If that email has a SafeNet student account, a password reset link was sent to it.',
    });
  } catch (error) {
    return next(error);
  }
}

async function resetStudentPassword(request, response, next) {
  try {
    const result = resetPasswordSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const { email } = await consumePasswordResetToken({ token: result.data.token, accountType: 'student' });
    const student = await Student.findOne({ email }).select('+password');
    if (!student) {
      return response.status(404).json({ success: false, message: 'This account no longer exists.' });
    }

    student.password = result.data.password;
    await student.save();
    // Students authenticate with bearer session tokens rather than a
    // sessionVersion counter, so ending every existing session after a
    // reset means deleting them outright — the same effect logout has.
    await StudentSession.deleteMany({ student: student._id });

    return response.json({ success: true, message: 'Password updated. You can now log in with your new password.' });
  } catch (error) {
    return next(error);
  }
}

module.exports = {
  currentStudent,
  forgotStudentPassword,
  loginStudent,
  logoutStudent,
  registerStudent,
  requestStudentRegistrationOtp,
  resetStudentPassword,
  verifyClassCode,
};
