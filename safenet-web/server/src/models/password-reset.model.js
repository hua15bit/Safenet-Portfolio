const mongoose = require('mongoose');

// Stores a short-lived, single-use password reset challenge. Only the hash of
// the token ever reaches MongoDB, the same way passwords are hashed, and a
// TTL index removes expired challenges without a cron job.
const passwordResetSchema = new mongoose.Schema(
  {
    email: {
      type: String,
      required: true,
      lowercase: true,
      trim: true,
      maxlength: 190,
    },
    accountType: {
      type: String,
      required: true,
      enum: ['student', 'professor'],
    },
    tokenHash: {
      type: String,
      required: true,
      select: false,
    },
    expiresAt: {
      type: Date,
      required: true,
    },
    lastSentAt: {
      type: Date,
      required: true,
    },
  },
  { timestamps: true, versionKey: false }
);

// One outstanding reset per account at a time — requesting a new link
// invalidates whatever link was sent before it.
passwordResetSchema.index({ email: 1, accountType: 1 }, { unique: true });
passwordResetSchema.index({ expiresAt: 1 }, { expireAfterSeconds: 0 });

module.exports = mongoose.model('PasswordReset', passwordResetSchema);
