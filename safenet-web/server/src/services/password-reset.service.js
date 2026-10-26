const crypto = require('node:crypto');

const PasswordReset = require('../models/password-reset.model');
const { sendPasswordResetLink } = require('./email.service');

const RESET_LIFETIME_MS = 30 * 60 * 1000;
const RESEND_COOLDOWN_MS = 60 * 1000;

function resetSecret() {
  const secret = process.env.OTP_SECRET || process.env.JWT_ACCESS_SECRET;
  if (!secret && process.env.NODE_ENV === 'production') {
    const error = new Error('Password reset is temporarily unavailable.');
    error.statusCode = 503;
    throw error;
  }
  return secret || 'local-development-only-otp-secret';
}

// Same HMAC reasoning as email verification: without the server's own secret,
// a leaked database alone can't be used to precompute working tokens.
function hashResetToken(token) {
  return crypto.createHmac('sha256', resetSecret()).update(token).digest('hex');
}

function resetBaseUrl() {
  const configured = process.env.CLIENT_ORIGIN || 'http://localhost:5173';
  let url;
  try {
    url = new URL(configured);
  } catch {
    const error = new Error('Password reset is temporarily unavailable.');
    error.statusCode = 503;
    throw error;
  }
  if (process.env.NODE_ENV === 'production' && url.protocol !== 'https:') {
    const error = new Error('Password reset is temporarily unavailable.');
    error.statusCode = 503;
    throw error;
  }
  return url.origin;
}

// Deliberately returns the same outcome whether or not `account` was found —
// unlike registration's "email already exists" checks, confirming or denying
// that a specific email has an account is exactly what a forgot-password
// endpoint should never leak, since it's a much more attractive target for
// account enumeration than registration is.
async function requestPasswordReset({ email, accountType, account }) {
  const normalizedEmail = email.trim().toLowerCase();
  if (!account) return;

  const existing = await PasswordReset.findOne({ email: normalizedEmail, accountType });
  if (existing && Date.now() - existing.lastSentAt.getTime() < RESEND_COOLDOWN_MS) {
    // Silently drop a too-soon repeat instead of telling the caller to slow
    // down — that response would itself confirm the account exists.
    return;
  }

  const token = crypto.randomBytes(32).toString('base64url');
  const tokenHash = hashResetToken(token);
  const now = new Date();
  await PasswordReset.findOneAndUpdate(
    { email: normalizedEmail, accountType },
    {
      $set: {
        tokenHash,
        expiresAt: new Date(now.getTime() + RESET_LIFETIME_MS),
        lastSentAt: now,
      },
    },
    { upsert: true, new: true, setDefaultsOnInsert: true }
  );

  const resetUrl = `${resetBaseUrl()}/reset-password?token=${encodeURIComponent(token)}&role=${accountType}`;
  try {
    await sendPasswordResetLink({ email: normalizedEmail, resetUrl, accountType });
  } catch (error) {
    // A delivery failure must not surface differently than the no-such-account
    // case above, or the difference itself would leak that the account exists.
    // The stored token is left in place so the operational fix (checking email
    // provider health) doesn't also force the user to request a new link.
    console.error(`Password reset link delivery failed for ${accountType}: ${error.message}`);
  }
}

// Verifies the token and, if valid, deletes it (single-use) before returning
// the email it belonged to. Callers still have to look up the actual account
// themselves — this only proves the emailed link is genuine and unexpired.
async function consumePasswordResetToken({ token, accountType }) {
  const tokenHash = hashResetToken(token);
  const match = await PasswordReset.findOne({ accountType, tokenHash }).select('+tokenHash');

  if (!match || match.expiresAt.getTime() <= Date.now()) {
    const error = new Error('This reset link is invalid or expired. Request a new one.');
    error.statusCode = 422;
    throw error;
  }

  await PasswordReset.deleteOne({ _id: match._id });
  return { email: match.email };
}

module.exports = { requestPasswordReset, consumePasswordResetToken };
