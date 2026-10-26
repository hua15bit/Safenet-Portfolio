const mongoose = require('mongoose');
// MongoDB structure for professor classes, schedules, and registration codes.

const meetingDays = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];

const classSchema = new mongoose.Schema(
  {
    professor: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Professor',
      required: [true, 'Professor is required.'],
      index: true,
    },
    subject: {
      type: String,
      required: true,
      trim: true,
      maxlength: 150,
      default: 'Living in the IT Era',
    },
    section: {
      type: String,
      required: [true, 'Section is required.'],
      trim: true,
      maxlength: 80,
    },
    schedule: {
      type: String,
      trim: true,
      maxlength: 120,
      default: null,
    },
    meetingDays: {
      type: [{ type: String, enum: meetingDays }],
      default: [],
    },
    startTime: {
      type: String,
      match: [/^([01]\d|2[0-3]):[0-5]\d$/, 'Start time must use HH:mm format.'],
      default: null,
    },
    endTime: {
      type: String,
      match: [/^([01]\d|2[0-3]):[0-5]\d$/, 'End time must use HH:mm format.'],
      default: null,
    },
    term: {
      type: String,
      trim: true,
      maxlength: 80,
      default: null,
    },
    classCode: {
      type: String,
      required: [true, 'Class code is required.'],
      unique: true,
      uppercase: true,
      trim: true,
      match: [/^[A-HJ-NP-Z2-9]{6}$/, 'Class code must contain 6 supported characters.'],
    },
    isActive: {
      type: Boolean,
      default: true,
      index: true,
    },
  },
  {
    timestamps: true,
    versionKey: false,
  }
);

classSchema.pre('validate', function validateSchedule() {
  if ((this.startTime && !this.endTime) || (!this.startTime && this.endTime)) {
    this.invalidate('startTime', 'Both start and end times are required.');
  }

  if (this.startTime && this.endTime && this.startTime >= this.endTime) {
    this.invalidate('endTime', 'End time must be later than start time.');
  }
});

classSchema.index({ professor: 1, startTime: 1, endTime: 1, isActive: 1 });
classSchema.index({ classCode: 1, isActive: 1 });

module.exports = mongoose.model('Class', classSchema);
