const crypto = require('node:crypto');

const EmailVerification = require('../models/email-verification.model');
const {
  sendRegistrationOtp,
  sendRegistrationVerificationLink,
} = require('./email.service');

const CODE_LIFETIME_MS = 10 * 60 * 1000;
const LINK_LIFETIME_MS = 15 * 60 * 1000;
const RESEND_COOLDOWN_MS = 60 * 1000;
const MAX_ATTEMPTS = 5;

function otpSecret() {
  const secret = process.env.OTP_SECRET || process.env.JWT_ACCESS_SECRET;
  if (!secret && process.env.NODE_ENV === 'production') {
    const error = new Error('Email verification is temporarily unavailable.');
    error.statusCode = 503;
    throw error;
  }
  return secret || 'local-development-only-otp-secret';
}

function hashOtp(email, code) {
  return crypto
    .createHmac('sha256', otpSecret())
    .update(`${email.toLowerCase()}:${code}`)
    .digest('hex');
}

function hashLinkSecret(kind, value) {
  return crypto
    .createHmac('sha256', otpSecret())
    .update(`${kind}:${value}`)
    .digest('hex');
}

function secretsMatch(storedHash, submittedHash) {
  if (!storedHash || !submittedHash || storedHash.length !== submittedHash.length) return false;
  return crypto.timingSafeEqual(
    Buffer.from(storedHash, 'hex'),
    Buffer.from(submittedHash, 'hex')
  );
}

async function requestRegistrationOtp({ email, purpose, accountType }) {
  const normalizedEmail = email.trim().toLowerCase();
  const existing = await EmailVerification.findOne({
    email: normalizedEmail,
    purpose,
  }).select('+codeHash');

  if (existing && Date.now() - existing.lastSentAt.getTime() < RESEND_COOLDOWN_MS) {
    const remainingSeconds = Math.ceil(
      (RESEND_COOLDOWN_MS - (Date.now() - existing.lastSentAt.getTime())) / 1000
    );
    const error = new Error(`Please wait ${remainingSeconds} seconds before requesting another code.`);
    error.statusCode = 429;
    throw error;
  }

  const code = String(crypto.randomInt(0, 1_000_000)).padStart(6, '0');
  await sendRegistrationOtp({ email: normalizedEmail, code, accountType });
  const now = new Date();
  await EmailVerification.findOneAndUpdate(
    { email: normalizedEmail, purpose },
    {
      $set: {
        codeHash: hashOtp(normalizedEmail, code),
        expiresAt: new Date(now.getTime() + CODE_LIFETIME_MS),
        lastSentAt: now,
        attempts: 0,
      },
    },
    { upsert: true, new: true, setDefaultsOnInsert: true }
  );
}

async function verifyRegistrationOtp({ email, purpose, code }) {
  const normalizedEmail = email.trim().toLowerCase();
  const record = await EmailVerification.findOne({
    email: normalizedEmail,
    purpose,
  }).select('+codeHash');

  if (!record || record.expiresAt.getTime() <= Date.now()) {
    const error = new Error('The verification code is missing or expired. Request a new code.');
    error.statusCode = 422;
    throw error;
  }
  if (record.attempts >= MAX_ATTEMPTS) {
    const error = new Error('Too many incorrect attempts. Request a new verification code.');
    error.statusCode = 429;
    throw error;
  }

  const submittedHash = hashOtp(normalizedEmail, code);
  const valid = crypto.timingSafeEqual(
    Buffer.from(record.codeHash, 'hex'),
    Buffer.from(submittedHash, 'hex')
  );
  if (!valid) {
    await EmailVerification.updateOne({ _id: record._id }, { $inc: { attempts: 1 } });
    const error = new Error('The verification code is incorrect.');
    error.statusCode = 422;
    throw error;
  }
}

async function consumeRegistrationOtp({ email, purpose }) {
  await EmailVerification.deleteOne({ email: email.trim().toLowerCase(), purpose });
}

function verificationBaseUrl() {
  const configured = process.env.CLIENT_ORIGIN || 'http://localhost:5173';
  let url;
  try {
    url = new URL(configured);
  } catch {
    const error = new Error('Email verification is temporarily unavailable.');
    error.statusCode = 503;
    throw error;
  }
  if (process.env.NODE_ENV === 'production' && url.protocol !== 'https:') {
    const error = new Error('Email verification is temporarily unavailable.');
    error.statusCode = 503;
    throw error;
  }
  return url.origin;
}

async function requestRegistrationLink({ email, purpose, accountType }) {
  const normalizedEmail = email.trim().toLowerCase();
  const existing = await EmailVerification.findOne({
    email: normalizedEmail,
    purpose,
  });

  if (existing && Date.now() - existing.lastSentAt.getTime() < RESEND_COOLDOWN_MS) {
    const remainingSeconds = Math.ceil(
      (RESEND_COOLDOWN_MS - (Date.now() - existing.lastSentAt.getTime())) / 1000
    );
    const error = new Error(`Please wait ${remainingSeconds} seconds before requesting another link.`);
    error.statusCode = 429;
    throw error;
  }

  const linkToken = crypto.randomBytes(32).toString('base64url');
  const browserProof = crypto.randomBytes(32).toString('base64url');
  const linkTokenHash = hashLinkSecret('email-link', linkToken);
  const browserProofHash = hashLinkSecret('browser-proof', browserProof);
  const now = new Date();
  const record = await EmailVerification.findOneAndUpdate(
    { email: normalizedEmail, purpose },
    {
      $set: {
        linkTokenHash,
        browserProofHash,
        verifiedAt: null,
        expiresAt: new Date(now.getTime() + LINK_LIFETIME_MS),
        lastSentAt: now,
        attempts: 0,
      },
      $unset: { codeHash: 1 },
    },
    { upsert: true, new: true, setDefaultsOnInsert: true }
  );

  const verificationUrl = `${verificationBaseUrl()}/verify-email?token=${encodeURIComponent(linkToken)}`;
  try {
    await sendRegistrationVerificationLink({
      email: normalizedEmail,
      verificationUrl,
      accountType,
    });
  } catch (error) {
    // Do not leave a usable challenge behind when delivery fails.
    await EmailVerification.deleteOne({ _id: record._id, linkTokenHash });
    throw error;
  }

  return {
    verificationId: record._id.toString(),
    verificationProof: browserProof,
    expiresInSeconds: Math.floor(LINK_LIFETIME_MS / 1000),
  };
}

async function confirmRegistrationLink(linkToken) {
  const linkTokenHash = hashLinkSecret('email-link', linkToken);
  const record = await EmailVerification.findOne({ linkTokenHash }).select('+linkTokenHash');
  if (!record || record.expiresAt.getTime() <= Date.now()) {
    const error = new Error('This verification link is invalid or expired. Request a new link.');
    error.statusCode = 422;
    throw error;
  }

  record.verifiedAt = record.verifiedAt || new Date();
  // Clearing the link hash makes the emailed URL one-use while retaining the
  // browser-bound proof needed to finish registration.
  record.linkTokenHash = undefined;
  await record.save();
  return { email: record.email, purpose: record.purpose };
}

async function verifyRegistrationLinkSession({ email, purpose, verificationId, verificationProof }) {
  const normalizedEmail = email.trim().toLowerCase();
  const record = await EmailVerification.findOne({
    _id: verificationId,
    email: normalizedEmail,
    purpose,
  }).select('+browserProofHash');
  const submittedHash = hashLinkSecret('browser-proof', verificationProof);
  if (
    !record ||
    !record.verifiedAt ||
    record.expiresAt.getTime() <= Date.now() ||
    !secretsMatch(record.browserProofHash, submittedHash)
  ) {
    const error = new Error('Open the verification link from your email, then try again.');
    error.statusCode = 422;
    throw error;
  }
}

module.exports = {
  confirmRegistrationLink,
  consumeRegistrationOtp,
  hashOtp,
  requestRegistrationLink,
  requestRegistrationOtp,
  verifyRegistrationLinkSession,
  verifyRegistrationOtp,
};
