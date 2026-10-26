const mongoose = require('mongoose');
const crypto = require('node:crypto');
// Contains the class-management rules used by the professor dashboard.
const { z } = require('zod');

const Class = require('../models/class.model');
const Student = require('../models/student.model');

const allowedDays = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];
const timePattern = /^([01]\d|2[0-3]):[0-5]\d$/;
const codeLetters = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
const codeDigits = '23456789';

const classFields = {
  subject: z.string().trim().min(1).max(150),
  section: z.string().trim().min(1).max(80),
  schedule: z.string().trim().max(120).nullable().optional(),
  meetingDays: z.array(z.enum(allowedDays)).min(1).max(7),
  startTime: z.string().regex(timePattern),
  endTime: z.string().regex(timePattern),
  term: z.string().trim().regex(
    /^(1st|2nd) Term \d{4}-\d{4}$/,
    'Select either the 1st or 2nd term and a valid academic year.'
  ),
};

const createClassSchema = z
  .strictObject(classFields)
  .refine((data) => data.endTime > data.startTime, {
    path: ['endTime'],
    message: 'End time must be later than start time.',
  });

const updateClassSchema = z
  .strictObject({
    subject: classFields.subject.optional(),
    section: classFields.section.optional(),
    schedule: classFields.schedule,
    meetingDays: classFields.meetingDays.optional(),
    startTime: classFields.startTime.optional(),
    endTime: classFields.endTime.optional(),
    term: classFields.term.optional(),
  })
  .refine((data) => Object.keys(data).length > 0, {
    message: 'Provide at least one field to update.',
  });

function validationFailure(response, result) {
  return response.status(422).json({
    success: false,
    message: 'Please check the submitted class information.',
    errors: result.error.issues.map((issue) => ({
      field: issue.path.join('.'),
      message: issue.message,
    })),
  });
}

function publicClass(classDocument) {
  return {
    id: classDocument._id.toString(),
    subject: classDocument.subject,
    section: classDocument.section,
    schedule: classDocument.schedule,
    meetingDays: classDocument.meetingDays,
    startTime: classDocument.startTime,
    endTime: classDocument.endTime,
    term: classDocument.term,
    code: classDocument.classCode,
    isActive: classDocument.isActive,
    createdAt: classDocument.createdAt,
    updatedAt: classDocument.updatedAt,
  };
}

async function generateClassCode() {
  // Generate a readable six-character code that is not already in use.
  for (let attempt = 0; attempt < 30; attempt += 1) {
    let code = '';
    for (let index = 0; index < 3; index += 1) {
      code += codeLetters[crypto.randomInt(codeLetters.length)];
    }
    for (let index = 0; index < 3; index += 1) {
      code += codeDigits[crypto.randomInt(codeDigits.length)];
    }

    if (!(await Class.exists({ classCode: code }))) return code;
  }

  throw new Error('A unique class code could not be generated.');
}

async function findScheduleConflict(professorId, classData, excludedClassId) {
  // Prevent one professor from creating overlapping class schedules.
  const query = {
    professor: professorId,
    isActive: true,
    startTime: { $lt: classData.endTime },
    endTime: { $gt: classData.startTime },
    meetingDays: { $in: classData.meetingDays },
  };

  if (excludedClassId) query._id = { $ne: excludedClassId };

  return Class.findOne(query);
}

async function listClasses(request, response, next) {
  try {
    const classes = await Class.find({
      professor: request.professor._id,
      isActive: true,
    }).sort({ createdAt: -1 });

    response.json({
      success: true,
      classes: classes.map(publicClass),
    });
  } catch (error) {
    next(error);
  }
}

async function createClass(request, response, next) {
  // Validate the schedule, create the class, and assign its registration code.
  try {
    const result = createClassSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const classData = {
      ...result.data,
      schedule: result.data.schedule || null,
      meetingDays: [...new Set(result.data.meetingDays)],
    };
    const conflict = await findScheduleConflict(request.professor._id, classData);

    if (conflict) {
      const days = classData.meetingDays.filter((day) => conflict.meetingDays.includes(day));
      return response.status(409).json({
        success: false,
        message: `Schedule conflict with ${conflict.section} on ${days.join(', ')}.`,
      });
    }

    const createdClass = await Class.create({
      ...classData,
      professor: request.professor._id,
      classCode: await generateClassCode(),
    });

    return response.status(201).json({
      success: true,
      message: 'Class created successfully.',
      class: publicClass(createdClass),
    });
  } catch (error) {
    return next(error);
  }
}

async function updateClass(request, response, next) {
  try {
    if (!mongoose.isValidObjectId(request.params.classId)) {
      return response.status(404).json({ success: false, message: 'Class not found.' });
    }

    const result = updateClassSchema.safeParse(request.body);
    if (!result.success) return validationFailure(response, result);

    const existingClass = await Class.findOne({
      _id: request.params.classId,
      professor: request.professor._id,
      isActive: true,
    });
    if (!existingClass) {
      return response.status(404).json({ success: false, message: 'Class not found.' });
    }

    const mergedData = {
      subject: result.data.subject ?? existingClass.subject,
      section: result.data.section ?? existingClass.section,
      schedule: result.data.schedule === undefined ? existingClass.schedule : result.data.schedule,
      meetingDays: result.data.meetingDays ?? existingClass.meetingDays,
      startTime: result.data.startTime ?? existingClass.startTime,
      endTime: result.data.endTime ?? existingClass.endTime,
      term: result.data.term ?? existingClass.term,
    };
    const completeResult = createClassSchema.safeParse(mergedData);
    if (!completeResult.success) return validationFailure(response, completeResult);

    const conflict = await findScheduleConflict(
      request.professor._id,
      completeResult.data,
      existingClass._id
    );
    if (conflict) {
      const days = mergedData.meetingDays.filter((day) => conflict.meetingDays.includes(day));
      return response.status(409).json({
        success: false,
        message: `Schedule conflict with ${conflict.section} on ${days.join(', ')}.`,
      });
    }

    Object.assign(existingClass, completeResult.data);
    await existingClass.save();

    return response.json({
      success: true,
      message: 'Class updated successfully.',
      class: publicClass(existingClass),
    });
  } catch (error) {
    return next(error);
  }
}

async function regenerateClassCode(request, response, next) {
  try {
    if (!mongoose.isValidObjectId(request.params.classId)) {
      return response.status(404).json({ success: false, message: 'Class not found.' });
    }

    const classDocument = await Class.findOne({
      _id: request.params.classId,
      professor: request.professor._id,
      isActive: true,
    });
    if (!classDocument) {
      return response.status(404).json({ success: false, message: 'Class not found.' });
    }

    classDocument.classCode = await generateClassCode();
    await classDocument.save();

    return response.json({
      success: true,
      message: 'Class code regenerated.',
      class: publicClass(classDocument),
    });
  } catch (error) {
    return next(error);
  }
}

async function deactivateClass(request, response, next) {
  // Soft-delete a class only when it no longer contains enrolled students.
  try {
    if (!mongoose.isValidObjectId(request.params.classId)) {
      return response.status(404).json({ success: false, message: 'Class not found.' });
    }

    const classDocument = await Class.findOne({
      _id: request.params.classId,
      professor: request.professor._id,
      isActive: true,
    });
    if (!classDocument) {
      return response.status(404).json({ success: false, message: 'Class not found.' });
    }

    const enrolledStudents = await Student.countDocuments({
      class: classDocument._id,
      status: 'enrolled',
    });
    if (enrolledStudents > 0) {
      return response.status(409).json({
        success: false,
        message: 'Remove or transfer enrolled students before deleting this class.',
      });
    }

    classDocument.isActive = false;
    await classDocument.save();

    return response.json({
      success: true,
      message: 'Class deactivated.',
    });
  } catch (error) {
    return next(error);
  }
}

module.exports = {
  createClass,
  deactivateClass,
  listClasses,
  regenerateClassCode,
  updateClass,
};
