// Email ownership checks can be disabled, use the archived six-digit code
// flow, or use the current one-click verification-link flow. Production stays
// disabled until its delivery credentials and mode are explicitly configured.
function emailVerificationMode() {
  const explicitMode = process.env.EMAIL_VERIFICATION_MODE?.trim().toLowerCase();
  if (['disabled', 'code', 'link'].includes(explicitMode)) return explicitMode;

  const legacyFlag = process.env.EMAIL_VERIFICATION_ENABLED;
  if (legacyFlag !== undefined) {
    return legacyFlag.trim().toLowerCase() === 'true' ? 'code' : 'disabled';
  }

  // Preserve the existing OTP behavior in development and automated tests.
  return process.env.NODE_ENV === 'production' ? 'disabled' : 'code';
}

function emailVerificationEnabled() {
  return emailVerificationMode() !== 'disabled';
}

module.exports = { emailVerificationEnabled, emailVerificationMode };
