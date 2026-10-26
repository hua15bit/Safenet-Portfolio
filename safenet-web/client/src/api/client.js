const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

// Sends requests to Express, includes login cookies, and reports API errors clearly.
export async function apiRequest(path, options = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    credentials: 'include',
    ...options,
    headers: {
      Accept: 'application/json',
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...options.headers,
    },
  });
  const result = await response.json().catch(() => null);
  if (!response.ok || !result?.success) {
    const error = new Error(result?.message || 'The SafeNet request could not be completed.');
    error.statusCode = response.status;
    throw error;
  }
  return result;
}
