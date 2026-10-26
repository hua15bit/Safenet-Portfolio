const crypto = require('node:crypto');
const { ipKeyGenerator, rateLimit } = require('express-rate-limit');

const WINDOW_MS = 15 * 60 * 1000;

// Keep limits configurable on Render without accepting invalid or zero values.
function positiveLimit(environmentName, fallback) {
  const parsed = Number(process.env[environmentName] ?? '');
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function requestIpKey(request) {
  const address = request.ip ?? request.socket?.remoteAddress ?? 'unknown';
  return `ip:${ipKeyGenerator(address)}`;
}

// Progress traffic is keyed by the student bearer token rather than the shared
// school Wi-Fi address. The token itself is never retained as a limiter key.
function studentProgressKey(request) {
  const authorization = request.get?.('authorization') ??
    request.headers?.authorization ?? '';
  const match = authorization.trim().match(/^Bearer\s+([a-fA-F0-9]{64})$/);
  if (!match) return requestIpKey(request);

  const digest = crypto
    .createHash('sha256')
    .update(match[1].toLowerCase())
    .digest('hex');
  return `student:${digest}`;
}

function limiter(limit, keyGenerator = requestIpKey, options = {}) {
  return rateLimit({
    windowMs: WINDOW_MS,
    limit,
    keyGenerator,
    standardHeaders: 'draft-8',
    legacyHeaders: false,
    message: {
      success: false,
      message: 'Too many requests. Please wait briefly and try again.',
    },
    ...options,
  });
}

// Failed attempts against one identity cannot consume the whole classroom's
// allowance. Hash identifiers so rate-limit storage does not retain emails.
function loginIdentityKey(request) {
  const identifier = request.body?.identifier || request.body?.email;
  const normalized = typeof identifier === 'string' ? identifier.trim().toLowerCase().slice(0, 190) : 'invalid';
  const digest = crypto.createHash('sha256').update(normalized).digest('hex');
  return `${requestIpKey(request)}:${digest}`;
}

const loginAttemptLimiter = limiter(10, loginIdentityKey, { skipSuccessfulRequests: true });
const registrationLimiter = limiter(120);
const classCodeLimiter = limiter(120);
// Keyed the same way as login attempts, so someone can't hammer one account
// with reset emails without also being bounded by their own identity+network.
const passwordResetLimiter = limiter(10, loginIdentityKey);

// Authentication remains protected per network, while authenticated score
// uploads are isolated per student so one classroom cannot block another.
const professorAuthLimiter = limiter(
  positiveLimit('PROFESSOR_AUTH_RATE_LIMIT', 120)
);
const studentAuthLimiter = limiter(
  positiveLimit('STUDENT_AUTH_RATE_LIMIT', 600)
);
const studentProgressLimiter = limiter(
  positiveLimit('STUDENT_PROGRESS_RATE_LIMIT', 300),
  studentProgressKey
);
const generalApiLimiter = limiter(
  positiveLimit('GENERAL_API_RATE_LIMIT', 6000)
);

module.exports = {
  loginAttemptLimiter,
  loginIdentityKey,
  registrationLimiter,
  classCodeLimiter,
  generalApiLimiter,
  passwordResetLimiter,
  positiveLimit,
  professorAuthLimiter,
  requestIpKey,
  studentAuthLimiter,
  studentProgressKey,
  studentProgressLimiter,
};
