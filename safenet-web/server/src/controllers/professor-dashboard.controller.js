const mongoose = require('mongoose');
// Collects dashboard data and manages professor-owned student records.
const { z } = require('zod');

const { MAX_GAME_SCORE, TOTAL_GRADED_MISSIONS } = require('../config/progress');
const Class = require('../models/class.model');
const GameRun = require('../models/game-run.model');
const MissionProgress = require('../models/mission-progress.model');
const Student = require('../models/student.model');

function classPayload(item) {
  return {
    id: item._id.toString(),
    subject: item.subject,
    section: item.section,
    schedule: item.schedule,
    meetingDays: item.meetingDays,
    startTime: item.startTime,
    endTime: item.endTime,
    term: item.term,
    code: item.classCode,
    createdAt: item.createdAt,
  };
}

function studentPayload(student) {
  return {
    id: student._id.toString(),
    classId: student.class._id.toString(),
    name: student.fullname,
    studentNumber: student.studentNumber,
    program: student.program,
    email: student.email,
    points: student.totalPoints,
    missionsCompleted: student.missionsCompleted,
    totalMissions: TOTAL_GRADED_MISSIONS,
    status: student.status,
    registeredAt: student.createdAt,
  };
}

async function dashboardState(request, response, next) {
  // Return the signed-in professor's active classes and enrolled students.
  try {
    const classes = await Class.find({
      professor: request.professor._id,
      isActive: true,
    }).sort({ createdAt: -1 });
    const classIds = classes.map((item) => item._id);
    const students = await Student.find({
      class: { $in: classIds },
      status: 'enrolled',
    })
      .populate('class')
      .sort({ fullname: 1 });

    response.json({
      success: true,
      professor: {
        id: request.professor._id.toString(),
        name: request.professor.fullname,
        email: request.professor.email,
        classes: classes.map(classPayload),
        students: students.map(studentPayload),
      },
    });
  } catch (error) {
    next(error);
  }
}

const enrollmentSchema = z.strictObject({
  fullname: z.string().trim().max(120).optional(),
  studentNumber: z.string().trim().min(5).max(30),
  classCode: z.string().trim().toUpperCase().length(6),
});

async function enrollStudent(request, response, next) {
  // Link an existing student record to one of the professor's classes.
  try {
    const result = enrollmentSchema.safeParse(request.body);
    if (!result.success) {
      return response.status(422).json({ success: false, message: 'Enter valid student and class information.' });
    }

    const classDocument = await Class.findOne({
      professor: request.professor._id,
      classCode: result.data.classCode,
      isActive: true,
    });
    if (!classDocument) {
      return response.status(404).json({ success: false, message: 'This code does not belong to one of your active classes.' });
    }

    const student = await Student.findOne({
      studentNumber: result.data.studentNumber.toUpperCase(),
      status: { $in: ['unenrolled', 'enrolled', 'inactive'] },
    });
    if (!student) {
      return response.status(404).json({ success: false, message: 'No registered student exists with that student number.' });
    }
    if (result.data.fullname && result.data.fullname.toLowerCase() !== student.fullname.toLowerCase()) {
      return response.status(422).json({ success: false, message: 'The student name does not match the registered account.' });
    }

    // A professor can claim an unassigned account or manage their own students,
    // but cannot move a student out of another professor's class.
    if (student.class) {
      const ownsCurrentClass = await Class.exists({
        _id: student.class,
        professor: request.professor._id,
      });
      if (!ownsCurrentClass) {
        return response.status(409).json({ success: false, message: 'This student is already assigned to another professor.' });
      }
    } else if (student.status !== 'unenrolled') {
      return response.status(409).json({ success: false, message: 'This student account needs its enrollment reviewed.' });
    }

    // Match the previous enrollment to avoid two professors claiming one account
    // at the same time. Updating only these fields preserves every game score.
    const enrolled = await Student.findOneAndUpdate(
      { _id: student._id, class: student.class ?? null, status: student.status },
      { $set: { class: classDocument._id, status: 'enrolled' } },
      { new: true, runValidators: true }
    );
    if (!enrolled) {
      return response.status(409).json({ success: false, message: 'This enrollment changed. Refresh the dashboard and try again.' });
    }
    return response.json({ success: true, message: 'Student enrolled successfully.' });
  } catch (error) {
    return next(error);
  }
}

const pointsSchema = z.strictObject({
  points: z.number().int().min(0).max(Number.MAX_SAFE_INTEGER),
  missionsCompleted: z.number().int().min(0).max(TOTAL_GRADED_MISSIONS),
});

async function updateStudentPoints(request, response, next) {
  // Keep an optional professor adjustment separate from Unity's earned score.
  try {
    if (!mongoose.isValidObjectId(request.params.studentId)) {
      return response.status(404).json({ success: false, message: 'Student record not found.' });
    }
    const result = pointsSchema.safeParse(request.body);
    if (!result.success) {
      return response.status(422).json({ success: false, message: 'Enter valid points and mission totals.' });
    }

    const ownedClasses = await Class.find({ professor: request.professor._id }).distinct('_id');
    const student = await Student.findOne({
      _id: request.params.studentId,
      class: { $in: ownedClasses },
      status: 'enrolled',
    });
    if (!student) {
      return response.status(404).json({ success: false, message: 'Student record not found.' });
    }

    const scoreResult = await MissionProgress.aggregate([
      { $match: { student: student._id } },
      { $group: { _id: null, total: { $sum: '$bestScore' } } },
    ]);
    const missionPoints = scoreResult[0]?.total ?? 0;
    student.points = missionPoints;
    student.manualPointsAdjustment = result.data.points - missionPoints;
    student.missionsCompleted = result.data.missionsCompleted;
    student.totalMissions = TOTAL_GRADED_MISSIONS;
    await student.save();

    return response.json({ success: true, message: 'Student performance updated.' });
  } catch (error) {
    return next(error);
  }
}

async function studentScoreHistory(request, response, next) {
  // Return only records belonging to a student in one of the professor's classes.
  try {
    if (!mongoose.isValidObjectId(request.params.studentId)) {
      return response.status(404).json({ success: false, message: 'Student record not found.' });
    }

    const ownedClasses = await Class.find({ professor: request.professor._id }).distinct('_id');
    const student = await Student.findOne({
      _id: request.params.studentId,
      class: { $in: ownedClasses },
      status: 'enrolled',
    });
    if (!student) {
      return response.status(404).json({ success: false, message: 'Student record not found.' });
    }

    const history = await GameRun.find({ student: student._id })
      .sort({ startedAt: -1 })
      .limit(100);

    return response.json({
      success: true,
      student: {
        id: student._id.toString(),
        name: student.fullname,
        currentPoints: student.totalPoints,
        maximumScore: MAX_GAME_SCORE,
      },
      history: history.map((run) => ({
        id: run._id.toString(),
        score: run.score,
        maximumScore: run.maximumScore,
        missionsCompleted: run.missionsCompleted,
        totalMissions: run.totalMissions,
        startedAt: run.startedAt,
        lastPlayedAt: run.lastPlayedAt,
        completedAt: run.completedAt,
      })),
    });
  } catch (error) {
    return next(error);
  }
}

async function removeStudent(request, response, next) {
  // Remove the selected student only from classes owned by this professor.
  try {
    if (!mongoose.isValidObjectId(request.params.studentId)) {
      return response.status(404).json({ success: false, message: 'Student record not found.' });
    }
    const ownedClasses = await Class.find({ professor: request.professor._id }).distinct('_id');
    const student = await Student.findOne({
      _id: request.params.studentId,
      class: { $in: ownedClasses },
      status: 'enrolled',
    });
    if (!student) {
      return response.status(404).json({ success: false, message: 'Student record not found.' });
    }

    student.status = 'inactive';
    await student.save();
    return response.json({ success: true, message: 'Student removed from active enrollment.' });
  } catch (error) {
    return next(error);
  }
}

module.exports = {
  dashboardState,
  enrollStudent,
  removeStudent,
  studentScoreHistory,
  updateStudentPoints,
};
