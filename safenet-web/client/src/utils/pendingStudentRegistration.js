// Bridges the registration tab and the email-link landing page (which may be
// a different tab opened from the mail app). Both share the same browser's
// localStorage, so the link can finish registration even if the original
// registration tab was closed or reloaded in the meantime.
export const PENDING_REGISTRATION_KEY = 'safenet.pendingStudentRegistration';
const MAX_AGE_MS = 20 * 60 * 1000; // link itself expires server-side after 15 minutes

export function savePendingStudentRegistration(data) {
  try {
    localStorage.setItem(PENDING_REGISTRATION_KEY, JSON.stringify({ ...data, savedAt: Date.now() }));
  } catch {
    // Private browsing / storage disabled: verification still works if the
    // original tab stays open, it just cannot be completed from elsewhere.
  }
}

export function loadPendingStudentRegistration(email) {
  try {
    const raw = localStorage.getItem(PENDING_REGISTRATION_KEY);
    if (!raw) return null;
    const draft = JSON.parse(raw);
    if (!draft || typeof draft !== 'object') return null;
    if (Date.now() - draft.savedAt > MAX_AGE_MS) return null;
    if (email && draft.email?.toLowerCase() !== email.toLowerCase()) return null;
    return draft;
  } catch {
    return null;
  }
}

export function clearPendingStudentRegistration() {
  try {
    localStorage.removeItem(PENDING_REGISTRATION_KEY);
  } catch {
    // Ignore.
  }
}
