const crypto = require('node:crypto');
const nodemailer = require('nodemailer');

let cachedTransporter;
let cachedTransportKey;

// SMTP credentials stay on the API server. They are never returned to the
// browser, written to MongoDB, or included in an Android build.
function requireEmailConfiguration() {
  const host = process.env.SMTP_HOST || 'smtp.gmail.com';
  const port = Number.parseInt(process.env.SMTP_PORT || '465', 10);
  const user = process.env.SMTP_USER;
  const password = process.env.SMTP_PASS;
  if (!Number.isInteger(port) || port < 1 || port > 65535 || !user || !password) {
    const error = new Error('Email verification is temporarily unavailable.');
    error.statusCode = 503;
    throw error;
  }

  return {
    host,
    port,
    secure: process.env.SMTP_SECURE
      ? process.env.SMTP_SECURE.trim().toLowerCase() === 'true'
      : port === 465,
    user,
    password,
    fromEmail: process.env.EMAIL_FROM_ADDRESS || user,
    fromName: process.env.EMAIL_FROM_NAME || 'SafeNet',
  };
}

function getTransporter(config) {
  const transportKey = `${config.host}:${config.port}:${config.secure}:${config.user}`;
  if (!cachedTransporter || cachedTransportKey !== transportKey) {
    cachedTransporter = nodemailer.createTransport({
      host: config.host,
      port: config.port,
      secure: config.secure,
      auth: { user: config.user, pass: config.password },
      connectionTimeout: 10_000,
      greetingTimeout: 10_000,
      socketTimeout: 15_000,
    });
    cachedTransportKey = transportKey;
  }
  return cachedTransporter;
}

function emailProvider() {
  return (process.env.EMAIL_DELIVERY_PROVIDER || 'smtp').trim().toLowerCase();
}

function safeHeader(value) {
  return String(value).replace(/[\r\n]+/g, ' ').trim();
}

function requireGmailApiConfiguration() {
  const config = {
    clientId: process.env.GMAIL_CLIENT_ID,
    clientSecret: process.env.GMAIL_CLIENT_SECRET,
    refreshToken: process.env.GMAIL_REFRESH_TOKEN,
    senderEmail: process.env.GMAIL_SENDER_EMAIL || process.env.EMAIL_FROM_ADDRESS,
    fromName: process.env.EMAIL_FROM_NAME || 'SafeNet',
  };
  if (!config.clientId || !config.clientSecret || !config.refreshToken || !config.senderEmail) {
    const error = new Error('Email verification is temporarily unavailable.');
    error.statusCode = 503;
    throw error;
  }
  return config;
}

async function gmailAccessToken(config) {
  const response = await fetch('https://oauth2.googleapis.com/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      client_id: config.clientId,
      client_secret: config.clientSecret,
      refresh_token: config.refreshToken,
      grant_type: 'refresh_token',
    }),
    signal: AbortSignal.timeout(15_000),
  });
  if (!response.ok) throw new Error(`Gmail authorization failed with HTTP ${response.status}.`);
  const data = await response.json();
  if (!data.access_token) throw new Error('Gmail authorization did not return an access token.');
  return data.access_token;
}

function gmailRawMessage({ fromName, fromEmail, to, subject, text, html }) {
  const boundary = `safenet-${crypto.randomUUID()}`;
  const message = [
    `From: ${safeHeader(fromName)} <${safeHeader(fromEmail)}>`,
    `To: ${safeHeader(to)}`,
    `Subject: ${safeHeader(subject)}`,
    'MIME-Version: 1.0',
    `Content-Type: multipart/alternative; boundary="${boundary}"`,
    '',
    `--${boundary}`,
    'Content-Type: text/plain; charset="UTF-8"',
    'Content-Transfer-Encoding: 8bit',
    '',
    text,
    `--${boundary}`,
    'Content-Type: text/html; charset="UTF-8"',
    'Content-Transfer-Encoding: 8bit',
    '',
    html,
    `--${boundary}--`,
  ].join('\r\n');
  return Buffer.from(message).toString('base64url');
}

async function sendWithGmailApi(message) {
  const config = requireGmailApiConfiguration();
  const accessToken = await gmailAccessToken(config);
  const response = await fetch('https://gmail.googleapis.com/gmail/v1/users/me/messages/send', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      raw: gmailRawMessage({
        ...message,
        fromName: config.fromName,
        fromEmail: config.senderEmail,
      }),
    }),
    signal: AbortSignal.timeout(15_000),
  });
  if (!response.ok) throw new Error(`Gmail delivery failed with HTTP ${response.status}.`);
}

async function sendEmail(message) {
  if (emailProvider() === 'gmail-api') return sendWithGmailApi(message);

  const config = requireEmailConfiguration();
  const result = await getTransporter(config).sendMail({
    from: { name: config.fromName, address: config.fromEmail },
    ...message,
  });
  if (result.rejected?.length) {
    throw new Error(`Recipient rejected: ${result.rejected.join(', ')}`);
  }
}

async function sendRegistrationOtp({ email, code, accountType }) {
  try {
    await sendEmail({
      to: email,
      subject: 'Your SafeNet verification code',
      text: `Your SafeNet ${accountType} verification code is ${code}. It expires in 10 minutes. Never share this code.`,
      html: `<div style="font-family:Arial,sans-serif;max-width:520px;margin:auto;padding:24px;color:#102a3a"><h1 style="color:#087db3">SafeNet email verification</h1><p>Use this code to finish creating your ${accountType} account:</p><p style="font-size:34px;font-weight:700;letter-spacing:8px">${code}</p><p>This code expires in 10 minutes. Never share it with anyone.</p></div>`,
    });
  } catch (providerError) {
    // Log operational metadata only; never log the App Password or OTP.
    console.error(`SMTP email delivery failed (${providerError.code || 'unknown'}): ${providerError.message}`);
    const error = new Error('The verification email could not be sent. Please try again.');
    error.statusCode = 502;
    throw error;
  }
}

async function sendRegistrationVerificationLink({ email, verificationUrl, accountType }) {
  try {
    await sendEmail({
      to: email,
      subject: 'Verify your SafeNet email address',
      text: `Open this link to verify your SafeNet ${accountType} account: ${verificationUrl}\n\nThe link expires in 15 minutes. If you did not request it, ignore this email.`,
      html: `<div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;padding:28px;color:#102a3a"><h1 style="color:#087db3">Verify your SafeNet email</h1><p>Confirm this email address to finish creating your ${accountType} account.</p><p style="margin:30px 0"><a href="${verificationUrl}" style="background:#087db3;color:#fff;text-decoration:none;padding:14px 22px;border-radius:5px;font-weight:700">VERIFY EMAIL ADDRESS</a></p><p>This one-use link expires in 15 minutes. If you did not request it, ignore this email.</p></div>`,
    });
  } catch (providerError) {
    // Operational details are useful on Render; credentials and tokens are
    // deliberately excluded from this message.
    console.error(`Email link delivery failed (${providerError.code || 'unknown'}): ${providerError.message}`);
    const error = new Error('The verification link could not be sent. Please try again.');
    error.statusCode = 502;
    throw error;
  }
}

async function sendPasswordResetLink({ email, resetUrl, accountType }) {
  try {
    await sendEmail({
      to: email,
      subject: 'Reset your SafeNet password',
      text: `Open this link to choose a new SafeNet ${accountType} password: ${resetUrl}\n\nThe link expires in 30 minutes. If you did not request this, ignore this email — your password will not change.`,
      html: `<div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;padding:28px;color:#102a3a"><h1 style="color:#087db3">Reset your SafeNet password</h1><p>Choose a new password for your ${accountType} account.</p><p style="margin:30px 0"><a href="${resetUrl}" style="background:#087db3;color:#fff;text-decoration:none;padding:14px 22px;border-radius:5px;font-weight:700">RESET PASSWORD</a></p><p>This one-use link expires in 30 minutes. If you did not request it, ignore this email — your password will not change.</p></div>`,
    });
  } catch (providerError) {
    console.error(`Password reset email delivery failed (${providerError.code || 'unknown'}): ${providerError.message}`);
    const error = new Error('The password reset email could not be sent. Please try again.');
    error.statusCode = 502;
    throw error;
  }
}

module.exports = { sendRegistrationOtp, sendRegistrationVerificationLink, sendPasswordResetLink };
