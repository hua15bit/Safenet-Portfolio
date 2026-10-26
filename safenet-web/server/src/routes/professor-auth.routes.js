const express = require('express');
// Maps professor account URLs to their authentication controllers.

const {
  currentProfessor,
  forgotProfessorPassword,
  loginProfessor,
  logoutProfessor,
  refreshProfessorSession,
  registerProfessor,
  resetProfessorPassword,
  requestProfessorRegistrationOtp,
} = require('../controllers/professor-auth.controller');
const requireProfessor = require('../middleware/auth.middleware');
const { loginAttemptLimiter, passwordResetLimiter, registrationLimiter } = require('../middleware/rate-limit.middleware');

const router = express.Router();

router.post('/register', registrationLimiter, registerProfessor);
router.post('/request-otp', requestProfessorRegistrationOtp);
router.post('/request-verification', requestProfessorRegistrationOtp);
router.post('/login', loginAttemptLimiter, loginProfessor);
router.get('/me', requireProfessor, currentProfessor);
router.post('/refresh', refreshProfessorSession);
router.post('/logout', logoutProfessor);
router.post('/forgot-password', passwordResetLimiter, forgotProfessorPassword);
router.post('/reset-password', passwordResetLimiter, resetProfessorPassword);

module.exports = router;
