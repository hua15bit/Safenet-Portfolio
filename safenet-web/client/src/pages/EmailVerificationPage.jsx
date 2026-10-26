import { useEffect, useState } from 'react';

import AppLink from '../components/AppLink';
import logo from '../assets/images/Logo.png';
import { apiRequest } from '../api/client';
import { loadPendingStudentRegistration, clearPendingStudentRegistration } from '../utils/pendingStudentRegistration';
import { loadPendingProfessorRegistration, clearPendingProfessorRegistration } from '../utils/pendingProfessorRegistration';
import '../styles/registration.css';

export default function EmailVerificationPage() {
  const [state, setState] = useState({ status: 'working', message: 'Confirming your email address...', purpose: null });
  const [createdStudent, setCreatedStudent] = useState(null);
  const [createdProfessor, setCreatedProfessor] = useState(null);

  useEffect(() => {
    const token = new URLSearchParams(window.location.search).get('token') || '';
    // Remove the secret from the visible URL and browser history immediately.
    window.history.replaceState({}, '', '/verify-email');
    if (!token) {
      setState({ status: 'error', message: 'This verification link is incomplete.', purpose: null });
      return;
    }

    apiRequest('/api/email-verification/confirm', {
      method: 'POST',
      body: JSON.stringify({ token }),
    })
      .then(async (result) => {
        // If this browser is the one that started registration (same device,
        // even if a different tab), finish the account creation right here —
        // no need to hunt down the original registration tab.
        if (result.purpose === 'student-registration') {
          await finishStudentRegistration(result);
        } else if (result.purpose === 'professor-registration') {
          await finishProfessorRegistration(result);
        } else {
          setState({ status: 'success', message: result.message, purpose: result.purpose || null });
        }
      })
      .catch((error) => setState({ status: 'error', message: error.message, purpose: null }));

    async function finishStudentRegistration(result) {
      const draft = loadPendingStudentRegistration(result.email);
      if (!draft) {
        setState({ status: 'success', message: result.message, purpose: result.purpose });
        return;
      }
      try {
        const registration = await apiRequest('/api/auth/students/register', {
          method: 'POST',
          body: JSON.stringify({
            fullname: draft.fullname,
            student_number: draft.student_number,
            program: draft.program,
            email: draft.email,
            password: draft.password,
            verification_id: draft.verification_id,
            verification_proof: draft.verification_proof,
            ...(draft.class_code ? { class_code: draft.class_code } : {}),
          }),
        });
        clearPendingStudentRegistration();
        setCreatedStudent(registration);
        setState({ status: 'success', message: 'Registration complete.', purpose: result.purpose });
      } catch (error) {
        clearPendingStudentRegistration();
        if (/already registered/i.test(error.message)) {
          // The original tab already finished creating the account.
          setState({ status: 'success', message: 'Your account is already set up.', purpose: result.purpose });
        } else {
          setState({ status: 'error', message: error.message, purpose: result.purpose });
        }
      }
    }

    async function finishProfessorRegistration(result) {
      const draft = loadPendingProfessorRegistration(result.email);
      if (!draft) {
        setState({ status: 'success', message: result.message, purpose: result.purpose });
        return;
      }
      try {
        const registration = await apiRequest('/api/auth/professors/register', {
          method: 'POST',
          body: JSON.stringify({
            fullname: draft.fullname,
            email: draft.email,
            password: draft.password,
            verification_id: draft.verification_id,
            verification_proof: draft.verification_proof,
          }),
        });
        clearPendingProfessorRegistration();
        setCreatedProfessor(registration.professor);
        setState({ status: 'success', message: 'Registration complete.', purpose: result.purpose });
      } catch (error) {
        clearPendingProfessorRegistration();
        if (/email already exists/i.test(error.message)) {
          // The original tab already finished creating the account.
          setState({ status: 'success', message: 'Your account is already set up.', purpose: result.purpose });
        } else {
          setState({ status: 'error', message: error.message, purpose: result.purpose });
        }
      }
    }
  }, []);

  const fallbackTarget = state.purpose === 'professor-registration' ? '/professor' : '/student/register';
  const fallbackLabel = state.purpose === 'professor-registration' ? 'GO TO PROFESSOR PORTAL' : 'GO TO REGISTRATION';

  return (
    <div className="registration-page email-verification-page">
      <div className="registration-bg" /><div className="registration-grid" />
      <header className="registration-header">
        <AppLink className="registration-brand" to="/"><img src={logo} alt="SafeNet logo" /><span>SAFENET</span></AppLink>
        <div className="system-status"><i /> EMAIL SECURITY NODE</div>
        <AppLink className="back-link" to="/">MAIN WEBSITE</AppLink>
      </header>
      <main className="verification-shell">
        {createdStudent ? (
          <section className="registration-panel success-state">
            <div className="success-icon" aria-hidden="true">✓</div>
            <p className="kicker">REGISTRATION COMPLETE</p>
            <h1>Profile initialized.</h1>
            <p role="status">{createdStudent.class
              ? 'Your account is connected and ready for SafeNet training missions.'
              : 'Your account is created and awaiting professor enrollment.'}</p>
            <div className="credential-card">
              <span>STUDENT OPERATIVE</span>
              <strong>{createdStudent.student.fullname}</strong>
              <p>Student number: {createdStudent.student.student_number}</p>
              {createdStudent.class ? <><p>{createdStudent.class.section}</p><code>{createdStudent.class.code}</code></>
                : <span className="pending-enrollment">AWAITING ENROLLMENT</span>}
            </div>
            <AppLink className="primary-button success-link" to="/">BACK TO SAFENET</AppLink>
          </section>
        ) : createdProfessor ? (
          <section className="registration-panel success-state">
            <div className="success-icon" aria-hidden="true">✓</div>
            <p className="kicker">REGISTRATION COMPLETE</p>
            <h1>Faculty account created.</h1>
            <p role="status">You are signed in as {createdProfessor.fullname}.</p>
            <AppLink className="primary-button success-link" to="/professor">GO TO PROFESSOR PORTAL</AppLink>
          </section>
        ) : (
          <section className={`registration-panel verification-result ${state.status}`}>
            <div className="success-icon" aria-hidden="true">{state.status === 'working' ? '…' : state.status === 'success' ? '✓' : '!'}</div>
            <p className="kicker">EMAIL VERIFICATION</p>
            <h1>{state.status === 'working' ? 'Checking link' : state.status === 'success' ? 'Email verified' : 'Link unavailable'}</h1>
            <p role={state.status === 'error' ? 'alert' : 'status'}>{state.message}</p>
            <p>{state.status === 'success'
              ? 'If you started registration on this device, switch back to that tab — it will finish automatically. Otherwise, continue below.'
              : state.status === 'error' ? 'Go back to registration and request a new verification link.' : 'Please wait.'}</p>
            {state.status !== 'working' && state.purpose ? (
              <AppLink className="primary-button success-link" to={fallbackTarget}>{fallbackLabel}</AppLink>
            ) : state.status !== 'working' ? (
              <div className="form-actions">
                <AppLink className="secondary-button" to="/student/register">STUDENT REGISTRATION</AppLink>
                <AppLink className="primary-button success-link" to="/professor">PROFESSOR PORTAL</AppLink>
              </div>
            ) : null}
          </section>
        )}
      </main>
    </div>
  );
}
