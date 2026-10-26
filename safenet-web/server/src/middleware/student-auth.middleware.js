const crypto = require('node:crypto');

const StudentSession = require('../models/student-session.model');
// Protects student/game routes by validating an active student session.

async function requireStudent(request, response, next) {
  // Attach the authenticated student and class to the request.
  try {
    const authorization = request.get('authorization') ?? '';
    const match = authorization.trim().match(/^Bearer\s+([a-fA-F0-9]{64})$/);

    if (!match) {
      return response.status(401).json({
        success: false,
        message: 'A valid access token is required.',
      });
    }

    const rawToken = match[1].toLowerCase();
    const tokenHash = crypto.createHash('sha256').update(rawToken).digest('hex');
    const session = await StudentSession.findOne({
      tokenHash,
      expiresAt: { $gt: new Date() },
    })
      .select('+tokenHash')
      .populate({
        path: 'student',
        match: { status: 'enrolled' },
        populate: {
          path: 'class',
          match: { isActive: true },
        },
      });

    if (!session?.student?.class) {
      return response.status(401).json({
        success: false,
        message: 'Your login has expired. Please sign in again.',
      });
    }

    session.lastUsedAt = new Date();
    await session.save();

    request.student = session.student;
    request.studentClass = session.student.class;
    request.studentSession = session;
    return next();
  } catch (error) {
    return next(error);
  }
}

module.exports = requireStudent;
