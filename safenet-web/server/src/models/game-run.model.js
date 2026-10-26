const mongoose = require('mongoose');

// Stores one summarized result for each authenticated Unity play session.
const gameRunSchema = new mongoose.Schema(
  {
    student: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Student',
      required: true,
      index: true,
    },
    session: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'StudentSession',
      required: true,
      unique: true,
    },
    missionScores: {
      type: Map,
      of: { type: Number, min: 0 },
      default: {},
    },
    completedMissions: {
      type: [String],
      default: [],
    },
    score: {
      type: Number,
      min: 0,
      default: 0,
    },
    maximumScore: {
      type: Number,
      min: 1,
      required: true,
    },
    missionsCompleted: {
      type: Number,
      min: 0,
      default: 0,
    },
    totalMissions: {
      type: Number,
      min: 1,
      required: true,
    },
    startedAt: {
      type: Date,
      required: true,
    },
    lastPlayedAt: {
      type: Date,
      required: true,
    },
    completedAt: {
      type: Date,
      default: null,
    },
  },
  {
    timestamps: true,
    versionKey: false,
  }
);

gameRunSchema.index({ student: 1, startedAt: -1 });

module.exports = mongoose.model('GameRun', gameRunSchema);
