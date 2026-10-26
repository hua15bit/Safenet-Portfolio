const cookieParser = require('cookie-parser');
// Builds the Express app and connects middleware to each API route group.
const cors = require('cors');
const express = require('express');
const helmet = require('helmet');
const requestSecurity = require('./middleware/request-security.middleware');

const healthRoutes = require('./routes/health.routes');
const emailVerificationRoutes = require('./routes/email-verification.routes');
const professorAuthRoutes = require('./routes/professor-auth.routes');
const classRoutes = require('./routes/class.routes');
const studentAuthRoutes = require('./routes/student-auth.routes');
const studentProgressRoutes = require('./routes/student-progress.routes');
const professorDashboardRoutes = require('./routes/professor-dashboard.routes');
const { errorHandler, notFoundHandler } = require('./middleware/error.middleware');
const {
  generalApiLimiter,
  professorAuthLimiter,
  studentAuthLimiter,
  studentProgressLimiter,
} = require('./middleware/rate-limit.middleware');

const app = express();

// Security, CORS, body parsing, cookies, and request-rate protection.
app.disable('x-powered-by');
if (process.env.NODE_ENV === 'production') app.set('trust proxy', 1);
app.use(helmet());
app.use(
  cors({
    origin: process.env.CLIENT_ORIGIN ?? 'http://localhost:5173',
    credentials: true,
  })
);
app.use(cookieParser());
app.use('/api', requestSecurity);
app.use(express.json({ limit: '16kb' }));

// Protect sensitive routes without treating a whole classroom behind one NAT
// address as one player. Health checks remain outside rate limiting so Render
// and deployment monitors can always read service status.
app.use('/api/health', healthRoutes);
app.use('/api/email-verification', generalApiLimiter, emailVerificationRoutes);
app.use('/api/auth/professors', professorAuthLimiter, professorAuthRoutes);
app.use('/api/auth/students', studentAuthLimiter);
// The generous network-wide guard blocks token-spamming abuse; the second
// limiter then gives each authenticated student an independent upload budget.
app.use(
  '/api/students/progress',
  generalApiLimiter,
  studentProgressLimiter
);
app.use('/api/classes/verify', generalApiLimiter);
app.use('/api', studentAuthRoutes);
app.use('/api', studentProgressRoutes);
app.use('/api/classes', generalApiLimiter, classRoutes);
app.use(
  '/api/professor/dashboard',
  generalApiLimiter,
  professorDashboardRoutes
);

// Return consistent JSON for unknown API addresses and unexpected errors.
app.use(notFoundHandler);
app.use(errorHandler);

module.exports = app;
