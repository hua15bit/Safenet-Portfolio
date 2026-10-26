// Handles API addresses that do not exist.
function notFoundHandler(request, response) {
  response.status(404).json({
    success: false,
    message: 'API route not found.',
  });
}

// Converts unexpected server errors into a consistent JSON response.
function errorHandler(error, _request, response, _next) {
  const candidate = error.statusCode ?? error.status ?? 500;
  const statusCode = Number.isInteger(candidate) && candidate >= 400 && candidate <= 599 ? candidate : 500;
  const parserMessages = {
    'entity.parse.failed': 'Invalid JSON request body.',
    'entity.too.large': 'Request body is too large.',
    'encoding.unsupported': 'Unsupported request encoding.',
    'charset.unsupported': 'Unsupported request encoding.',
  };

  if (process.env.NODE_ENV !== 'test') {
    // Parser errors can contain passwords/body fragments; log metadata only.
    console.error('API request failed', { status: statusCode, type: error.type || error.name });
  }

  response.status(statusCode).json({
    success: false,
    message: statusCode >= 500 ? 'Internal server error.' : parserMessages[error.type] || error.message,
  });
}

module.exports = {
  errorHandler,
  notFoundHandler,
};
