// Saves authenticated Unity mission results and derives student totals server-side.
const { z } = require('zod');

const {
  GRADED_MISSION_KEYS,
  MAX_GAME_SCORE,
  MAX_MISSION_SCORE,
  TOTAL_GRADED_MISSIONS,
} = require('../config/progress');
const GameRun = require('../models/game-run.model');
const MissionProgress = require('../models/mission-progress.model');
const Student = require('../models/student.model');
const studentPayload = require('../utils/student-payload');

const progressSchema = z.strictObject({
  mission_key: z.enum(GRADED_MISSION_KEYS),
  mission_score: z.number().int().min(0).max(MAX_MISSION_SCORE),
  completed: z.boolean(),
});

function missionPayload(mission) {
  return {
    mission_key: mission.missionKey,
    best_score: mission.bestScore,
    completed: mission.completed,
    completed_at: mission.completedAt,
  };
}

async function synchronizeStudentTotals(studentId) {
  // The database records one best score per mission, preventing replay inflation.
  const summary = await MissionProgress.aggregate([
    { $match: { student: studentId, missionKey: { $in: GRADED_MISSION_KEYS } } },
    {
      $group: {
        _id: null,
        points: { $sum: '$bestScore' },
        missionsCompleted: { $sum: { $cond: ['$completed', 1, 0] } },
      },
    },
  ]);

  return Student.findByIdAndUpdate(
    studentId,
    {
      points: summary[0]?.points ?? 0,
      missionsCompleted: summary[0]?.missionsCompleted ?? 0,
      totalMissions: TOTAL_GRADED_MISSIONS,
    },
    { returnDocument: 'after', runValidators: true }
  ).populate('class');
}

async function synchronizeGameRun(request, data) {
  // A login token identifies one complete play session. Mission details stay
  // internal while the professor sees one dated score summary for that run.
  const now = new Date();
  const update = {
    $setOnInsert: {
      student: request.student._id,
      session: request.studentSession._id,
      maximumScore: MAX_GAME_SCORE,
      totalMissions: TOTAL_GRADED_MISSIONS,
      startedAt: request.studentSession.createdAt ?? now,
    },
    $set: { lastPlayedAt: now },
    $max: { [`missionScores.${data.mission_key}`]: data.mission_score },
  };
  if (data.completed) {
    update.$addToSet = { completedMissions: data.mission_key };
  }

  const run = await GameRun.findOneAndUpdate(
    { student: request.student._id, session: request.studentSession._id },
    update,
    // Avoid inserting the Map's empty default in the same update that writes a
    // missionScores.<area> path; MongoDB correctly rejects conflicting paths.
    { returnDocument: 'after', upsert: true, runValidators: true, setDefaultsOnInsert: false }
  );
  const scores = run.missionScores instanceof Map
    ? [...run.missionScores.values()]
    : Object.values(run.missionScores ?? {});
  run.score = scores.reduce((total, score) => total + score, 0);
  run.missionsCompleted = (run.completedMissions ?? []).length;
  if (run.missionsCompleted >= TOTAL_GRADED_MISSIONS && !run.completedAt) {
    run.completedAt = now;
  }
  await run.save();
}

async function saveMissionProgress(request, response, next) {
  try {
    const result = progressSchema.safeParse(request.body);
    if (!result.success) {
      return response.status(422).json({
        success: false,
        message: 'Enter a valid SafeNet mission result.',
        errors: result.error.issues.map((issue) => ({
          field: issue.path.join('.'),
          message: issue.message,
        })),
      });
    }

    const data = result.data;
    const update = {
      $max: { bestScore: data.mission_score },
      $setOnInsert: {
        student: request.student._id,
        missionKey: data.mission_key,
      },
    };
    if (data.completed) {
      update.$set = { completed: true, completedAt: new Date() };
    }

    const mission = await MissionProgress.findOneAndUpdate(
      { student: request.student._id, missionKey: data.mission_key },
      update,
      { returnDocument: 'after', upsert: true, runValidators: true, setDefaultsOnInsert: true }
    );
    const student = await synchronizeStudentTotals(request.student._id);
    await synchronizeGameRun(request, data);

    return response.json({
      success: true,
      message: 'Mission progress synchronized.',
      mission: missionPayload(mission),
      student: studentPayload(student, student.class),
    });
  } catch (error) {
    return next(error);
  }
}

async function currentProgress(request, response, next) {
  try {
    const missions = await MissionProgress.find({
      student: request.student._id,
      missionKey: { $in: GRADED_MISSION_KEYS },
    }).sort({ missionKey: 1 });
    const student = await synchronizeStudentTotals(request.student._id);

    return response.json({
      success: true,
      student: studentPayload(student, student.class),
      missions: missions.map(missionPayload),
    });
  } catch (error) {
    return next(error);
  }
}

module.exports = { currentProgress, saveMissionProgress };
