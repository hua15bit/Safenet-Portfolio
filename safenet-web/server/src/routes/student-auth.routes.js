const express = require('express');
// Maps student registration, login, class verification, and session URLs.

const {
  currentStudent,
  forgotStudentPassword,
  loginStudent,
  logoutStudent,
  registerStudent,
  requestStudentRegistrationOtp,
  resetStudentPassword,
  verifyClassCode,
} = require('../controllers/student-auth.controller');
const requireStudent = require('../middleware/student-auth.middleware');
const { loginAttemptLimiter, passwordResetLimiter, registrationLimiter, classCodeLimiter } = require('../middleware/rate-limit.middleware');

const router = express.Router();

router.get('/classes/verify', classCodeLimiter, verifyClassCode);
router.post('/auth/students/request-otp', requestStudentRegistrationOtp);
router.post('/auth/students/request-verification', requestStudentRegistrationOtp);
router.post('/auth/students/register', registrationLimiter, registerStudent);
router.post('/auth/students/login', loginAttemptLimiter, loginStudent);
router.get('/students/me', requireStudent, currentStudent);
router.post('/auth/students/logout', requireStudent, logoutStudent);
router.post('/auth/students/forgot-password', passwordResetLimiter, forgotStudentPassword);
router.post('/auth/students/reset-password', passwordResetLimiter, resetStudentPassword);

module.exports = router;
