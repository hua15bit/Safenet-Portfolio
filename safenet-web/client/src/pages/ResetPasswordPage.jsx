import { useEffect, useState } from 'react';

import AppLink from '../components/AppLink';
import PasswordInput from '../components/PasswordInput';
import logo from '../assets/images/Logo.png';
import { apiRequest } from '../api/client';
import '../styles/registration.css';

const MIN_LENGTH = { professor: 6, student: 8 };

// What loads when the link from a password-reset email is opened. The token
// (and which kind of account it belongs to) travel as query params; both are
// stripped from the visible URL immediately, the same way the email
// verification link does, so the secret doesn't linger in browser history.
export default function ResetPasswordPage() {
  const [token, setToken] = useState('');
  const [role, setRole] = useState('professor');
  const [status, setStatus] = useState('form');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const linkToken = params.get('token') || '';
    const linkRole = params.get('role') === 'student' ? 'student' : 'professor';
    window.history.replaceState({}, '', '/reset-password');
    setToken(linkToken);
    setRole(linkRole);
    if (!linkToken) {
      setStatus('error');
      setMessage('This reset link is incomplete. Request a new one.');
    }
  }, []);

  async function submit(event) {
    event.preventDefault();
    setMessage('');
    const minLength = MIN_LENGTH[role];
    if (password.length < minLength) {
      setMessage(`Use at least ${minLength} characters.`);
      return;
    }
    if (password !== confirmPassword) {
      setMessage('Passwords do not match.');
      return;
    }

    setLoading(true);
    try {
      await apiRequest(`/api/auth/${role}s/reset-password`, {
        method: 'POST',
        body: JSON.stringify({ token, password }),
      });
      setStatus('success');
    } catch (error) {
      setMessage(error.message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="registration-page email-verification-page">
      <div className="registration-bg" /><div className="registration-grid" />
      <header className="registration-header">
        <AppLink className="registration-brand" to="/"><img src={logo} alt="SafeNet logo" /><span>SAFENET</span></AppLink>
        <div className="system-status"><i /> EMAIL SECURITY NODE</div>
        <AppLink className="back-link" to="/">MAIN WEBSITE</AppLink>
      </header>
      <main className="verification-shell">
        {status === 'success' ? (
          <section className="registration-panel success-state">
            <div className="success-icon" aria-hidden="true">✓</div>
            <p className="kicker">PASSWORD UPDATED</p>
            <h1>You're all set.</h1>
            <p role="status">
              {role === 'professor'
                ? 'Your professor account password has been changed. You can now log in with it.'
                : 'Your student account password has been changed. Sign in with it the next time you open SafeNet on Android.'}
            </p>
            {role === 'professor'
              ? <AppLink className="primary-button success-link" to="/professor">GO TO PROFESSOR PORTAL</AppLink>
              : <AppLink className="primary-button success-link" to="/">BACK TO SAFENET</AppLink>}
          </section>
        ) : status === 'error' ? (
          <section className="registration-panel verification-result error">
            <div className="success-icon" aria-hidden="true">!</div>
            <p className="kicker">PASSWORD RESET</p>
            <h1>Link unavailable</h1>
            <p role="alert">{message}</p>
            <div className="form-actions">
              <AppLink className="secondary-button" to="/student/register">STUDENT REGISTRATION</AppLink>
              <AppLink className="primary-button success-link" to="/professor">PROFESSOR PORTAL</AppLink>
            </div>
          </section>
        ) : (
          <section className="registration-panel">
            <div className="form-heading">
              <p className="kicker">PASSWORD RESET</p>
              <h2>Choose a new password</h2>
              <p>{role === 'professor' ? 'For your SafeNet professor account.' : 'For your SafeNet student account.'}</p>
            </div>
            <form onSubmit={submit}>
              <div className="field-grid">
                <label>New Password
                  <PasswordInput value={password} onChange={(event) => setPassword(event.target.value)} placeholder={`Minimum ${MIN_LENGTH[role]} characters`} autoComplete="new-password" required />
                </label>
                <label>Confirm Password
                  <PasswordInput value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} placeholder="Repeat password" autoComplete="new-password" required />
                </label>
              </div>
              {message && <div className="form-message" role="alert">{message}</div>}
              <div className="form-actions">
                <button className="primary-button" disabled={loading}>{loading ? 'UPDATING...' : 'UPDATE PASSWORD'}</button>
              </div>
            </form>
          </section>
        )}
      </main>
    </div>
  );
}
