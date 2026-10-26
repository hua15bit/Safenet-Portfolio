import { useEffect, useState } from 'react';

import { apiRequest } from '../api/client';

// Reads the API's runtime mode so enabling verification on Render does not
// require a separate static-site rebuild or risk client/server disagreement.
export default function useEmailVerificationMode() {
  const [mode, setMode] = useState('disabled');

  useEffect(() => {
    let active = true;
    apiRequest('/api/email-verification/config')
      .then((result) => {
        if (active && ['disabled', 'code', 'link'].includes(result.mode)) setMode(result.mode);
      })
      .catch(() => {
        // Registration remains usable in the API's safe disabled mode. If the
        // server requires verification, it will reject an unverified request.
      });
    return () => { active = false; };
  }, []);

  return mode;
}
