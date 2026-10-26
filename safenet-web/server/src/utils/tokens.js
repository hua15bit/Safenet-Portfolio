const jwt = require('jsonwebtoken');
// Creates, stores, and verifies professor JWT login tokens.

const accessCookieName = 'safenet_access';
const refreshCookieName = 'safenet_refresh';

function requireSecret(name) {
  const secret = process.env[name];

  if (!secret || secret.length < 32) {
    throw new Error(`${name} must contain at least 32 characters.`);
  }

  return secret;
}

function createAccessToken(professorId, version = 0) {
  return jwt.sign(
    { sub: professorId.toString(), role: 'professor', type: 'access', ver: version },
    requireSecret('JWT_ACCESS_SECRET'),
    { expiresIn: '15m', algorithm: 'HS256' }
  );
}

function createRefreshToken(professorId, version = 0) {
  return jwt.sign(
    { sub: professorId.toString(), role: 'professor', type: 'refresh', ver: version },
    requireSecret('JWT_REFRESH_SECRET'),
    { expiresIn: '7d', algorithm: 'HS256' }
  );
}

function cookieOptions(maxAge) {
  // httpOnly prevents browser JavaScript from reading authentication cookies.
  const production = process.env.NODE_ENV === 'production';
  return {
    httpOnly: true,
    secure: production,
    sameSite: production ? 'none' : 'lax',
    partitioned: production,
    maxAge,
    path: '/',
  };
}

function setAuthCookies(response, accessToken, refreshToken) {
  response.cookie(accessCookieName, accessToken, cookieOptions(15 * 60 * 1000));
  response.cookie(refreshCookieName, refreshToken, cookieOptions(7 * 24 * 60 * 60 * 1000));
}

function clearAuthCookies(response) {
  const options = cookieOptions(0);
  delete options.maxAge;
  response.clearCookie(accessCookieName, options);
  response.clearCookie(refreshCookieName, options);
}

function verifyAccessToken(token) {
  const payload = jwt.verify(token, requireSecret('JWT_ACCESS_SECRET'), { algorithms: ['HS256'] });

  if (payload.type !== 'access' || payload.role !== 'professor') {
    throw new Error('Invalid access token.');
  }

  return payload;
}

function verifyRefreshToken(token) {
  const payload = jwt.verify(token, requireSecret('JWT_REFRESH_SECRET'), { algorithms: ['HS256'] });

  if (payload.type !== 'refresh' || payload.role !== 'professor') {
    throw new Error('Invalid refresh token.');
  }

  return payload;
}

module.exports = {
  accessCookieName,
  clearAuthCookies,
  createAccessToken,
  createRefreshToken,
  refreshCookieName,
  setAuthCookies,
  verifyAccessToken,
  verifyRefreshToken,
};
