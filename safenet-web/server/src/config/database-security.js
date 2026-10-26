// Production database traffic must use verified TLS, even if someone later
// supplies a connection string with weaker transport options in Render.
function databaseOptions(uri, production = process.env.NODE_ENV === 'production') {
  if (!production) return {};
  if (!/^mongodb(?:\+srv)?:\/\//i.test(uri || '')) {
    throw new Error('A valid MongoDB connection string is required.');
  }
  const query = new URLSearchParams((uri.split('?')[1] || '').split('#')[0]);
  for (const [name, value] of query) {
    const key = name.toLowerCase();
    const enabled = value.toLowerCase();
    if ((['tls', 'ssl'].includes(key) && enabled !== 'true') ||
        (['tlsinsecure', 'tlsallowinvalidcertificates', 'tlsallowinvalidhostnames'].includes(key) && enabled !== 'false')) {
      throw new Error('Production database connections require verified TLS.');
    }
  }
  return { tls: true };
}

module.exports = databaseOptions;
