const { z } = require('zod');
// Handles professor registration, login, session refresh, profile, and logout.

const Professor = require('../models/professor.model');
const { emailVerificationMode } = require('../config/email-verification');
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
const {
  clearAuthCookies,
  accessCookieName,
  createAccessToken,
  createRefreshToken,
  refreshCookieName,
  setAuthCookies,
  verifyRefreshToken,
  verifyAccessToken,
} = require('../utils/tokens');

const registerSchema = z.strictObject({
  website: z.string().max(0, 'Unable to register.').optional(),
  fullname: z.string().trim().min(2).max(120),
  email: z.string().trim().email().max(190),
  password: z.string().min(6).max(128).refine((value) => Buffer.byteLength(value, 'utf8') <= 72, 'Password must be at most 72 UTF-8 bytes.'),
  otp: z.string().trim().regex(/^\d{6}$/, 'Enter the six-digit verification code.').optional(),
  verification_id: z.string().trim().regex(/^[a-fA-F0-9]{24}$/).optional(),
  verification_proof: z.string().trim().regex(/^[A-Za-z0-9_-]{43}$/).optional(),
});

const otpRequestSchema = z.strictObject({
  email: z.string().trim().email().max(190),
});

const forgotPasswordSchema = z.strictObject({
  email: z.string().trim().email().max(190),
});

const resetPasswordSchema = z.strictObject({
  token: z.string().trim().min(1).max(200),
  password: z.string().min(6).max(128).refine((value) => Buffer.byteLength(value, 'utf8') <= 72, 'Password must be at most 72 UTF-8 bytes.'),
});

const loginSchema = z.strictObject({
  email: z.string().trim().email().max(190),
  password: z.string().min(1).max(128),
});

function publicProfessor(professor) {
  return {
    id: professor._id.toString(),
    fullname: professor.fullname,
    email: professor.email,
    createdAt: professor.createdAt,
  };
}

function validationFailure(response, result) {
  return response.status(422).json({
    success: false,
    message: 'Please check the submitted information.',
    errors: result.error.issues.map((issue) => ({
      field: issue.path.join('.'),
      message: issue.message,
    })),
  });
}

function issueTokens(response, professor) {
  // Create short- and long-lived tokens, then store them in secure cookies.
  const accessToken = createAccessToken(professor._id, professor.sessionVersion ?? 0);
  const refreshToken = createRefreshToken(professor._id, professor.sessionVersion ?? 0);
  setAuthCookies(response, accessToken, refreshToken);
  return accessToken;
}

async function registerProfessor(request, response, next) {
  // Validate the form, reject duplicate email addresses, and create the account.
  try {
    const result = registerSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const email = result.data.email.toLowerCase();
    const existingProfessor = await Professor.exists({ email });

    if (existingProfessor) {
      return response.status(409).json({
        success: false,
        message: 'Email already exists.',
      });
    }

    const verificationMode = emailVerificationMode();
    if (verificationMode === 'link') {
      if (!result.data.verification_id || !result.data.verification_proof) {
        return response.status(422).json({
          success: false,
          message: 'Send and open the email verification link before creating your account.',
        });
      }
      await verifyRegistrationLinkSession({
        email,
        purpose: 'professor-registration',
        verificationId: result.data.verification_id,
        verificationProof: result.data.verification_proof,
      });
    } else if (verificationMode === 'code') {
      if (!result.data.otp) {
        return response.status(422).json({
          success: false,
          message: 'Enter the six-digit verification code.',
        });
      }
      await verifyRegistrationOtp({
        email,
        purpose: 'professor-registration',
        code: result.data.otp,
      });
    }

    const professor = await Professor.create({
      fullname: result.data.fullname,
      email,
      password: result.data.password,
    });
    if (verificationMode !== 'disabled') {
      await consumeRegistrationOtp({ email, purpose: 'professor-registration' });
    }
    const accessToken = issueTokens(response, professor);

    return response.status(201).json({
      success: true,
      message: 'Registration successful.',
      professor: publicProfessor(professor),
      accessToken,
    });
  } catch (error) {
    if (error?.code === 11000) {
      return response.status(409).json({
        success: false,
        message: 'Email already exists.',
      });
    }
    return next(error);
  }
}

async function requestProfessorRegistrationOtp(request, response, next) {
  // Verify ownership of the professor email before creating the account.
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
    if (await Professor.exists({ email })) {
      return response.status(409).json({
        success: false,
        message: 'Email already exists.',
      });
    }

    if (verificationMode === 'link') {
      const session = await requestRegistrationLink({
        email,
        purpose: 'professor-registration',
        accountType: 'professor',
      });
      return response.json({
        success: true,
        message: 'A verification link was sent to your email. Open it, then return here.',
        ...session,
      });
    }

    await requestRegistrationOtp({
      email,
      purpose: 'professor-registration',
      accountType: 'professor',
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

async function loginProfessor(request, response, next) {
  // Check the submitted credentials and start an authenticated session.
  try {
    const result = loginSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const professor = await Professor.findOne({
      email: result.data.email.toLowerCase(),
    }).select('+password');

    if (!professor || !(await professor.comparePassword(result.data.password))) {
      return response.status(401).json({
        success: false,
        message: 'Invalid email or password.',
      });
    }

    const accessToken = issueTokens(response, professor);

    return response.json({
      success: true,
      message: 'Login successful.',
      professor: publicProfessor(professor),
      accessToken,
    });
  } catch (error) {
    return next(error);
  }
}

function currentProfessor(request, response) {
  response.json({
    success: true,
    professor: publicProfessor(request.professor),
  });
}

async function refreshProfessorSession(request, response) {
  try {
    const refreshToken = request.cookies[refreshCookieName];
    if (!refreshToken) throw new Error('Refresh token is missing.');

    const payload = verifyRefreshToken(refreshToken);
    const professor = await Professor.findById(payload.sub);
    if (!professor || (payload.ver ?? 0) !== (professor.sessionVersion ?? 0)) {
      throw new Error('Professor session no longer exists.');
    }

    const accessToken = issueTokens(response, professor);
    return response.json({ success: true, accessToken });
  } catch (_error) {
    clearAuthCookies(response);
    return response.status(401).json({
      success: false,
      message: 'Your session has expired. Please log in again.',
    });
  }
}

async function logoutProfessor(request, response, next) {
  // Expire server-side authority as well as browser cookies. Signing out also
  // ends other professor sessions; a copied refresh token cannot revive them.
  try {
    let payload;
    const refresh = request.cookies?.[refreshCookieName];
    const access = request.get('authorization')?.replace(/^Bearer /, '') || request.cookies?.[accessCookieName];
    try {
      if (refresh) payload = verifyRefreshToken(refresh);
      else if (access) payload = verifyAccessToken(access);
    } catch { /* An already expired/invalid token can still clear its cookies. */ }
    if (payload) {
      const version = payload.ver ?? 0;
      await Professor.updateOne(
        { _id: payload.sub, ...(version === 0
          ? { $or: [{ sessionVersion: 0 }, { sessionVersion: { $exists: false } }] }
          : { sessionVersion: version }) },
        { $inc: { sessionVersion: 1 } }
      );
    }
  } catch (error) {
    clearAuthCookies(response);
    return next(error);
  }
  clearAuthCookies(response);
  response.json({
    success: true,
    message: 'Logged out.',
  });
}

// Always answers the same way whether or not the email belongs to an account
// — see password-reset.service.js for why that matters here specifically.
async function forgotProfessorPassword(request, response, next) {
  try {
    const result = forgotPasswordSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const email = result.data.email.toLowerCase();
    const account = await Professor.exists({ email });
    await requestPasswordReset({ email, accountType: 'professor', account });

    return response.json({
      success: true,
      message: 'If that email has a SafeNet professor account, a password reset link was sent to it.',
    });
  } catch (error) {
    return next(error);
  }
}

async function resetProfessorPassword(request, response, next) {
  try {
    const result = resetPasswordSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const { email } = await consumePasswordResetToken({ token: result.data.token, accountType: 'professor' });
    const professor = await Professor.findOne({ email }).select('+password');
    if (!professor) {
      return response.status(404).json({ success: false, message: 'This account no longer exists.' });
    }

    professor.password = result.data.password;
    // A password reset ends every existing session, the same way logout
    // does — a token issued before the reset should not keep working.
    professor.sessionVersion = (professor.sessionVersion ?? 0) + 1;
    await professor.save();

    return response.json({ success: true, message: 'Password updated. You can now log in with your new password.' });
  } catch (error) {
    return next(error);
  }
}

module.exports = {
  currentProfessor,
  forgotProfessorPassword,
  loginProfessor,
  logoutProfessor,
  refreshProfessorSession,
  registerProfessor,
  resetProfessorPassword,
  requestProfessorRegistrationOtp,
};
