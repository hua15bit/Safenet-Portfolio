const bcrypt = require('bcrypt');
// MongoDB structure and password behavior for student accounts.
const mongoose = require('mongoose');

const studentSchema = new mongoose.Schema(
  {
    class: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Class',
      // Code-free registration waits for a professor to assign a class.
      required: [function requiresClass() { return this.status === 'enrolled'; }, 'Enrolled students require a class.'],
      default: null,
      index: true,
    },
    fullname: {
      type: String,
      required: [true, 'Full name is required.'],
      trim: true,
      minlength: 2,
      maxlength: 120,
    },
    studentNumber: {
      type: String,
      required: [true, 'Student number is required.'],
      unique: true,
      trim: true,
      uppercase: true,
      maxlength: 30,
    },
    program: {
      type: String,
      required: [true, 'Program is required.'],
      trim: true,
      maxlength: 120,
    },
    email: {
      type: String,
      required: [true, 'Email address is required.'],
      unique: true,
      trim: true,
      lowercase: true,
      maxlength: 190,
      match: [/^[^\s@]+@[^\s@]+\.[^\s@]+$/, 'Enter a valid email address.'],
    },
    password: {
      type: String,
      required: [true, 'Password is required.'],
      minlength: 6,
      select: false,
    },
    points: {
      type: Number,
      min: 0,
      default: 0,
    },
    manualPointsAdjustment: {
      type: Number,
      default: 0,
    },
    missionsCompleted: {
      type: Number,
      min: 0,
      default: 0,
    },
    totalMissions: {
      type: Number,
      min: 1,
      default: 6,
    },
    status: {
      type: String,
      enum: ['unenrolled', 'enrolled', 'inactive'],
      default: 'enrolled',
      index: true,
    },
  },
  {
    timestamps: true,
    versionKey: false,
  }
);

studentSchema.pre('save', async function hashPassword() {
  if (!this.isModified('password')) {
    return;
  }

  this.password = await bcrypt.hash(this.password, 12);
});

studentSchema.methods.comparePassword = function comparePassword(candidatePassword) {
  return bcrypt.compare(candidatePassword, this.password);
};

studentSchema.virtual('totalPoints').get(function totalPoints() {
  return Math.max(0, this.points + this.manualPointsAdjustment);
});

studentSchema.set('toJSON', {
  virtuals: true,
  transform: (_document, returnedObject) => {
    delete returnedObject.password;
    return returnedObject;
  },
});

module.exports = mongoose.model('Student', studentSchema);
