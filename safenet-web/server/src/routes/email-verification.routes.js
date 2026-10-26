const express = require('express');

const {
  confirmEmailVerificationLink,
  getEmailVerificationConfiguration,
} = require('../controllers/email-verification.controller');
const { registrationLimiter } = require('../middleware/rate-limit.middleware');

const router = express.Router();

router.get('/config', getEmailVerificationConfiguration);
router.post('/confirm', registrationLimiter, confirmEmailVerificationLink);

module.exports = router;
