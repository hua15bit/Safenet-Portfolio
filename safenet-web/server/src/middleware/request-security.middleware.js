const { accessCookieName, refreshCookieName } = require('../utils/tokens');

// CORS alone prevents reading responses, not submitting a forged cookie request.
// Browser writes must come from our configured frontend. Native Unity requests
// do not carry Origin/cookies and continue using the existing bearer contract.
function requestSecurity(request, response, next) {
  response.set('Cache-Control', 'no-store');
  const origin = request.get('origin');
  const trustedOrigin = process.env.CLIENT_ORIGIN || 'http://localhost:5173';
  if (origin && origin !== trustedOrigin) {
    return response.status(403).json({ success: false, message: 'Request origin is not allowed.' });
  }
  if (['GET', 'HEAD', 'OPTIONS'].includes(request.method)) return next();

  const hasAuthCookie = request.cookies?.[accessCookieName] || request.cookies?.[refreshCookieName];
  if (!origin && (hasAuthCookie || request.get('sec-fetch-site') === 'cross-site')) {
    return response.status(403).json({ success: false, message: 'A trusted browser origin is required.' });
  }

  // No user file uploads exist in SafeNet. Reject multipart, plain text, and
  // browser form posts; JSON also forces a CORS preflight for browser requests.
  const hasBody = Number(request.get('content-length') || 0) > 0 || request.get('transfer-encoding');
  if (hasBody && !request.is('application/json')) {
    return response.status(415).json({ success: false, message: 'Use application/json for API requests.' });
  }
  return next();
}

module.exports = requestSecurity;
