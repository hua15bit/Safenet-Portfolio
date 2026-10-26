const express = require('express');
// Professor-only URLs for creating, editing, listing, and deactivating classes.

const {
  createClass,
  deactivateClass,
  listClasses,
  regenerateClassCode,
  updateClass,
} = require('../controllers/class.controller');
const requireProfessor = require('../middleware/auth.middleware');

const router = express.Router();

router.use(requireProfessor);
router.get('/', listClasses);
router.post('/', createClass);
router.patch('/:classId', updateClass);
router.post('/:classId/regenerate-code', regenerateClassCode);
router.delete('/:classId', deactivateClass);

module.exports = router;
