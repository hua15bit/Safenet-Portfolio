const dns = require('node:dns');
const mongoose = require('mongoose');
const databaseOptions = require('./database-security');

// Opens the MongoDB Atlas connection shared by all database models. Safe to
// call on every request: on a persistent server this only ever runs once at
// boot, but on serverless hosting (Vercel) each invocation calls it again, so
// an already-connected or already-connecting state must be reused rather than
// re-opening the connection.
async function connectDatabase() {
  if (mongoose.connection.readyState === 1 || mongoose.connection.readyState === 2) {
    return;
  }

  const mongoUri = process.env.MONGODB_URI;

  if (!mongoUri) {
    throw new Error('MONGODB_URI is missing from server/.env');
  }

  if (mongoUri.startsWith('mongodb+srv://')) {
    dns.setServers(['1.1.1.1', '8.8.8.8']);
  }

  mongoose.connection.on('connected', () => {
    console.log('MongoDB connection established.');
  });

  mongoose.connection.on('error', (error) => {
    // Driver error messages can include connection details. Do not log secrets.
    console.error('MongoDB connection error:', error.name);
  });

  mongoose.connection.on('disconnected', () => {
    console.warn('MongoDB connection closed.');
  });

  await mongoose.connect(mongoUri, {
    ...databaseOptions(mongoUri),
    dbName: process.env.MONGODB_DATABASE ?? 'safenet',
    serverSelectionTimeoutMS: 10000,
  });
}

module.exports = connectDatabase;
