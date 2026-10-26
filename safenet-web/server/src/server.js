require('dotenv').config();
// Server entry point: loads private settings, connects to MongoDB, and starts Express.

const app = require('./app');
const connectDatabase = require('./config/database');

const port = Number(process.env.PORT) || 5000;

async function startServer() {
  // Start accepting requests only after the database connection succeeds.
  try {
    await connectDatabase();

    const server = app.listen(port, '0.0.0.0', () => {
      console.log(`SafeNet API listening at http://localhost:${port}`);
    });

    function shutDown(signal) {
      console.log(`${signal} received. Closing SafeNet API.`);
      server.close(() => process.exit(0));
    }

    process.on('SIGINT', () => shutDown('SIGINT'));
    process.on('SIGTERM', () => shutDown('SIGTERM'));
  } catch (error) {
    console.error('SafeNet API failed to start. Check private server configuration.', { type: error.name });
    process.exit(1);
  }
}

startServer();
