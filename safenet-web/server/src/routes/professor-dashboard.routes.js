const express = require('express');
// Professor-only URLs for dashboard data and student management.

const {
  dashboardState,
  enrollStudent,
  removeStudent,
  studentScoreHistory,
  updateStudentPoints,
} = require('../controllers/professor-dashboard.controller');
const requireProfessor = require('../middleware/auth.middleware');

const router = express.Router();
router.use(requireProfessor);
router.get('/', dashboardState);
router.post('/students/enroll', enrollStudent);
router.get('/students/:studentId/score-history', studentScoreHistory);
router.patch('/students/:studentId/points', updateStudentPoints);
router.delete('/students/:studentId', removeStudent);

module.exports = router;
