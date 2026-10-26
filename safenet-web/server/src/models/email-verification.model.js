const mongoose = require('mongoose');

// Stores short-lived verification challenges. Secrets are hashed before they
// reach MongoDB, and expired challenges are removed by the TTL index.
const emailVerificationSchema = new mongoose.Schema(
  {
    email: {
      type: String,
      required: true,
      lowercase: true,
      trim: true,
      maxlength: 190,
    },
    purpose: {
      type: String,
      required: true,
      enum: ['student-registration', 'professor-registration'],
    },
    codeHash: {
      type: String,
      select: false,
    },
    linkTokenHash: {
      type: String,
      select: false,
    },
    browserProofHash: {
      type: String,
      select: false,
    },
    verifiedAt: {
      type: Date,
      default: null,
    },
    expiresAt: {
      type: Date,
      required: true,
    },
    lastSentAt: {
      type: Date,
      required: true,
    },
    attempts: {
      type: Number,
      default: 0,
      min: 0,
    },
  },
  { timestamps: true, versionKey: false }
);

emailVerificationSchema.index({ email: 1, purpose: 1 }, { unique: true });
emailVerificationSchema.index({ expiresAt: 1 }, { expireAfterSeconds: 0 });

module.exports = mongoose.model('EmailVerification', emailVerificationSchema);
