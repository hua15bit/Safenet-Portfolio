// Same purpose as pendingStudentRegistration.js, for the professor portal's
// registration flow: bridges the registration tab and the email-link landing
// page via localStorage, since they may end up in different tabs.
export const PENDING_PROFESSOR_REGISTRATION_KEY = 'safenet.pendingProfessorRegistration';
const MAX_AGE_MS = 20 * 60 * 1000; // link itself expires server-side after 15 minutes

export function savePendingProfessorRegistration(data) {
  try {
    localStorage.setItem(PENDING_PROFESSOR_REGISTRATION_KEY, JSON.stringify({ ...data, savedAt: Date.now() }));
  } catch {
    // Private browsing / storage disabled: verification still works if the
    // original tab stays open, it just cannot be completed from elsewhere.
  }
}

export function loadPendingProfessorRegistration(email) {
  try {
    const raw = localStorage.getItem(PENDING_PROFESSOR_REGISTRATION_KEY);
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

export function clearPendingProfessorRegistration() {
  try {
    localStorage.removeItem(PENDING_PROFESSOR_REGISTRATION_KEY);
  } catch {
    // Ignore.
  }
}
