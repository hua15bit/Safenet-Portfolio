const { z } = require('zod');

const { emailVerificationMode } = require('../config/email-verification');
const { confirmRegistrationLink } = require('../services/email-verification.service');

const confirmationSchema = z.strictObject({
  token: z.string().trim().min(40).max(100),
});

function getEmailVerificationConfiguration(_request, response) {
  response.json({
    success: true,
    mode: emailVerificationMode(),
  });
}

async function confirmEmailVerificationLink(request, response, next) {
  try {
    if (emailVerificationMode() !== 'link') {
      return response.status(503).json({
        success: false,
        message: 'Email-link verification is not currently enabled.',
      });
    }
    const result = confirmationSchema.safeParse(request.body);
    if (!result.success) {
      return response.status(422).json({
        success: false,
        message: 'This verification link is invalid.',
      });
    }

    const { email, purpose } = await confirmRegistrationLink(result.data.token);
    return response.json({
      success: true,
      message: 'Email verified.',
      email,
      purpose,
    });
  } catch (error) {
    return next(error);
  }
}

module.exports = {
  confirmEmailVerificationLink,
  getEmailVerificationConfiguration,
};
