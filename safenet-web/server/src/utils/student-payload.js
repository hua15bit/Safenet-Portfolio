// Selects the safe student and class fields returned to the mobile client.
const { TOTAL_GRADED_MISSIONS } = require('../config/progress');

function studentPayload(student, classDocument) {
  return {
    id: student._id.toString(),
    fullname: student.fullname,
    student_number: student.studentNumber,
    program: student.program,
    email: student.email,
    points: student.totalPoints,
    missions_completed: student.missionsCompleted,
    total_missions: TOTAL_GRADED_MISSIONS,
    class: {
      id: classDocument._id.toString(),
      subject: classDocument.subject,
      section: classDocument.section,
      term: classDocument.term,
      class_code: classDocument.classCode,
    },
  };
}

module.exports = studentPayload;
