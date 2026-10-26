const express = require('express');
// Health route used to confirm that the API and database are online.
const mongoose = require('mongoose');

const router = express.Router();

router.get('/', (_request, response) => {
  const databaseStates = ['disconnected', 'connected', 'connecting', 'disconnecting'];
  const databaseStatus = databaseStates[mongoose.connection.readyState] ?? 'unknown';

  response.status(200).json({
    success: true,
    message: 'SafeNet API is running.',
    database: databaseStatus,
    timestamp: new Date().toISOString(),
  });
});

module.exports = router;
