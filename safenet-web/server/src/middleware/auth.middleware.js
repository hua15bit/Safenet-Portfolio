const Professor = require('../models/professor.model');
// Protects professor-only API routes by validating an access token.
const { accessCookieName, verifyAccessToken } = require('../utils/tokens');

async function requireProfessor(request, response, next) {
  // Attach the authenticated professor to the request for the controller.
  try {
    const authorization = request.get('authorization');
    const bearerToken = authorization?.startsWith('Bearer ') ? authorization.slice(7) : null;
    const token = bearerToken || request.cookies[accessCookieName];

    if (!token) {
      return response.status(401).json({
        success: false,
        message: 'Authentication is required.',
      });
    }

    const payload = verifyAccessToken(token);
    const professor = await Professor.findById(payload.sub);

    if (!professor || (payload.ver ?? 0) !== (professor.sessionVersion ?? 0)) {
      return response.status(401).json({
        success: false,
        message: 'Your session has ended. Please log in again.',
      });
    }

    request.professor = professor;
    return next();
  } catch (_error) {
    return response.status(401).json({
      success: false,
      message: 'Your session is invalid or has expired.',
    });
  }
}

module.exports = requireProfessor;
