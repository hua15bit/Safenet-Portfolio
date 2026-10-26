const mongoose = require('mongoose');
// Stores each student's best result for every graded Unity mission.

const missionProgressSchema = new mongoose.Schema(
  {
    student: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Student',
      required: [true, 'Student is required.'],
      index: true,
    },
    missionKey: {
      type: String,
      required: [true, 'Mission key is required.'],
      trim: true,
      maxlength: 50,
    },
    bestScore: {
      type: Number,
      min: 0,
      default: 0,
    },
    completed: {
      type: Boolean,
      default: false,
      index: true,
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

missionProgressSchema.pre('save', function synchronizeCompletionDate() {
  if (this.completed && !this.completedAt) {
    this.completedAt = new Date();
  }

  if (!this.completed) {
    this.completedAt = null;
  }
});

missionProgressSchema.index({ student: 1, missionKey: 1 }, { unique: true });
missionProgressSchema.index({ student: 1, completed: 1 });

module.exports = mongoose.model('MissionProgress', missionProgressSchema);
