const express = require('express');

const {
  currentProgress,
  saveMissionProgress,
} = require('../controllers/student-progress.controller');
const requireStudent = require('../middleware/student-auth.middleware');

const router = express.Router();

router.get('/students/progress', requireStudent, currentProgress);
router.post('/students/progress', requireStudent, saveMissionProgress);

module.exports = router;
