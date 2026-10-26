const bcrypt = require('bcrypt');
// MongoDB structure and password behavior for professor accounts.
const mongoose = require('mongoose');

const professorSchema = new mongoose.Schema(
  {
    // Revokes previously issued login/refresh tokens when the user signs out.
    sessionVersion: { type: Number, default: 0, min: 0 },
    fullname: {
      type: String,
      required: [true, 'Full name is required.'],
      trim: true,
      minlength: [2, 'Full name must contain at least 2 characters.'],
      maxlength: [120, 'Full name cannot exceed 120 characters.'],
    },
    email: {
      type: String,
      required: [true, 'Email address is required.'],
      unique: true,
      trim: true,
      lowercase: true,
      maxlength: [190, 'Email address cannot exceed 190 characters.'],
      match: [/^[^\s@]+@[^\s@]+\.[^\s@]+$/, 'Enter a valid email address.'],
    },
    password: {
      type: String,
      required: [true, 'Password is required.'],
      minlength: [6, 'Password must contain at least 6 characters.'],
      select: false,
    },
  },
  {
    timestamps: true,
    versionKey: false,
  }
);

professorSchema.pre('save', async function hashPassword() {
  if (!this.isModified('password')) {
    return;
  }

  this.password = await bcrypt.hash(this.password, 12);
});

professorSchema.methods.comparePassword = function comparePassword(candidatePassword) {
  return bcrypt.compare(candidatePassword, this.password);
};

professorSchema.set('toJSON', {
  transform: (_document, returnedObject) => {
    delete returnedObject.password;
    delete returnedObject.sessionVersion;
    return returnedObject;
  },
});

const Professor = mongoose.model('Professor', professorSchema);

module.exports = Professor;
