import { useEffect, useState } from 'react';
// Three-step student registration: identity, optional class, and password.
import AppLink from '../components/AppLink';
import PasswordInput from '../components/PasswordInput';
import RegistrationBotTrap from '../components/RegistrationBotTrap';
import useEmailVerificationMode from '../hooks/useEmailVerificationMode';
import logo from '../assets/images/Logo.png';
import { apiRequest } from '../api/client';
import {
  PENDING_REGISTRATION_KEY,
  savePendingStudentRegistration,
  clearPendingStudentRegistration,
} from '../utils/pendingStudentRegistration';
import '../styles/registration.css';

const initialForm = { fullname: '', student_number: '', program: '', email: '', class_code: '', password: '', confirmPassword: '', otp: '', consent: false };
const educationPrograms = [
  ['BEEd', 'Bachelor of Elementary Education'],
  ['BSEd', 'Bachelor of Secondary Education'],
  ['BSE', 'Bachelor of Science in Education'],
  ['BECED', 'Bachelor of Early Childhood Education'],
  ['BSNEd', 'Bachelor of Special Needs Education'],
  ['BPEd', 'Bachelor of Physical Education'],
  ['BTVTEd', 'Bachelor of Technical-Vocational Teacher Education'],
];

export default function StudentRegistrationPage() {
  // `view` switches the whole right-hand panel between the registration
  // wizard and the forgot-password form; `step` only matters within 'register'.
  const [view, setView] = useState('register');
  const verificationMode = useEmailVerificationMode();
  const emailVerificationEnabled = verificationMode !== 'disabled';
  const [step, setStep] = useState(1);
  const [form, setForm] = useState(initialForm);
  const [verifiedClass, setVerifiedClass] = useState(null);
  const [message, setMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const [created, setCreated] = useState(null);
  const [otpStatus, setOtpStatus] = useState('');
  const [verificationSession, setVerificationSession] = useState(null);
  const [botTrap, setBotTrap] = useState('');
  const [forgotEmail, setForgotEmail] = useState('');
  const [forgotNotice, setForgotNotice] = useState('');
  const [forgotMessage, setForgotMessage] = useState('');

  useEffect(() => { document.title = 'SafeNet | Student Registration'; }, []);

  const update = (event) => {
    const { name, type, checked, value } = event.target;
    const nextValue = name === 'class_code'
      ? value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 6)
      : name === 'otp' ? value.replace(/\D/g, '').slice(0, 6)
        : type === 'checkbox' ? checked : value;
    setForm((current) => ({ ...current, [name]: nextValue }));
    if (name === 'email') setVerificationSession(null);
    if (name === 'class_code') setVerifiedClass(null);
    setMessage('');
  };

  // The email link may be opened in a different tab (e.g. the mail app's
  // in-app browser), so the data needed to finish registration is mirrored to
  // localStorage — shared across tabs in this browser — instead of living
  // only in this component's memory.
  function savePendingRegistration(verificationId, verificationProof) {
    savePendingStudentRegistration({
      email: form.email.trim().toLowerCase(),
      fullname: form.fullname.trim(),
      student_number: form.student_number.trim(),
      program: form.program,
      password: form.password,
      class_code: form.class_code || undefined,
      verification_id: verificationId,
      verification_proof: verificationProof,
    });
  }

  async function verifyCode() {
    if (form.class_code.length !== 6) throw new Error('Enter the complete six-character class code.');
    const result = await apiRequest(`/api/classes/verify?code=${encodeURIComponent(form.class_code)}`);
    setVerifiedClass(result.class);
    return result.class;
  }

  function skipClass() {
    if (loading) return;
    // Explicitly clear any partially entered/previously verified class code.
    setForm((current) => ({ ...current, class_code: '' }));
    setVerifiedClass(null);
    setMessage('');
    setStep(3);
  }

  async function next() {
    if (loading) return;
    setMessage('');
    if (step === 1) {
      if (form.fullname.trim().length < 3 || !/^[A-Za-z0-9-]{5,20}$/.test(form.student_number) || !form.program || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
        setMessage('Complete all student information using valid school details.');
        return;
      }
      setStep(2);
      return;
    }
    if (step === 2) {
      setLoading(true);
      try { await verifyCode(); setStep(3); } catch (error) { setMessage(error.message); } finally { setLoading(false); }
      return;
    }
    if (step === 3 && (form.password.length < 8 || form.password !== form.confirmPassword || !form.consent)) {
      setMessage(form.password !== form.confirmPassword ? 'Passwords do not match.' : 'Use at least 8 characters and confirm the authorization statement.');
      return;
    }
    if (step === 3) {
      if (!emailVerificationEnabled) {
        await createStudentAccount();
        return;
      }
      // Open the verification screen before requesting the email so the code
      // field and retry action remain available if the provider is unavailable.
      setStep(4);
      setLoading(true);
      try {
        const result = await apiRequest('/api/auth/students/request-verification', {
          method: 'POST', body: JSON.stringify({ email: form.email.trim().toLowerCase() }),
        });
        if (verificationMode === 'link') {
          setVerificationSession({
            id: result.verificationId,
            proof: result.verificationProof,
          });
          savePendingRegistration(result.verificationId, result.verificationProof);
        } else {
          setVerificationSession({ codeRequested: true });
        }
        setOtpStatus(result.message);
      } catch (error) {
        setVerificationSession(null);
        setMessage(error.message);
      } finally { setLoading(false); }
      return;
    }
    if (verificationMode === 'code' && form.otp.length !== 6) {
      setMessage('Enter the six-digit verification code from your email.');
      return;
    }
    if (verificationMode === 'link' && !verificationSession) {
      setMessage('Send the verification link before creating your account.');
      return;
    }
    await createStudentAccount();
  }

  async function createStudentAccount() {
    setLoading(true);
    try {
      const result = await apiRequest('/api/auth/students/register', {
        method: 'POST',
        body: JSON.stringify({
          fullname: form.fullname.trim(),
          student_number: form.student_number.trim(),
          program: form.program,
          email: form.email.trim().toLowerCase(),
          password: form.password,
          website: botTrap,
          ...(verificationMode === 'code' ? { otp: form.otp } : {}),
          ...(verificationMode === 'link' && verificationSession ? {
            verification_id: verificationSession.id,
            verification_proof: verificationSession.proof,
          } : {}),
          ...(form.class_code ? { class_code: form.class_code } : {}),
        }),
      });
      setCreated(result);
      clearPendingStudentRegistration();
      setForm(initialForm); // Do not retain the password after registration.
    } catch (error) {
      if (/already registered/i.test(error.message) && verificationSession) {
        // Most likely means the email-link tab already finished registration.
        setCreated({
          student: { fullname: form.fullname.trim(), student_number: form.student_number.trim() },
          class: verifiedClass ? { section: verifiedClass.section, subject: verifiedClass.subject, code: form.class_code } : null,
        });
        clearPendingStudentRegistration();
        setForm(initialForm);
      } else {
        setMessage(error.message);
      }
    } finally {
      setLoading(false);
    }
  }

  // If the email link finished registration in another tab, this tab notices
  // the shared draft disappear and stops waiting on its own.
  useEffect(() => {
    function onStorage(event) {
      if (event.key !== PENDING_REGISTRATION_KEY || event.newValue) return;
      if (step === 4 && verificationMode === 'link' && verificationSession && !created && !loading) {
        createStudentAccount();
      }
    }
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  });

  async function resendOtp() {
    if (loading) return;
    setLoading(true); setMessage(''); setOtpStatus('');
    try {
      const result = await apiRequest('/api/auth/students/request-verification', {
        method: 'POST', body: JSON.stringify({ email: form.email.trim().toLowerCase() }),
      });
      if (verificationMode === 'link') {
        setVerificationSession({
          id: result.verificationId,
          proof: result.verificationProof,
        });
        savePendingRegistration(result.verificationId, result.verificationProof);
      } else {
        setVerificationSession({ codeRequested: true });
      }
      setOtpStatus(result.message);
    } catch (error) {
      setVerificationSession(null);
      setMessage(error.message);
    } finally { setLoading(false); }
  }

  async function requestForgotPassword(event) {
    event.preventDefault();
    setForgotMessage(''); setForgotNotice('');
    if (!/^\S+@\S+\.\S+$/.test(forgotEmail.trim())) {
      setForgotMessage('Enter a valid email address.');
      return;
    }
    setLoading(true);
    try {
      const result = await apiRequest('/api/auth/students/forgot-password', {
        method: 'POST',
        body: JSON.stringify({ email: forgotEmail.trim().toLowerCase() }),
      });
      setForgotNotice(result.message);
    } catch (error) {
      setForgotMessage(error.message);
    } finally {
      setLoading(false);
    }
  }

  function backToRegistration() {
    setView('register');
    setForgotEmail('');
    setForgotNotice('');
    setForgotMessage('');
  }

  const strength = [form.password.length >= 8, /[A-Z]/.test(form.password), /\d/.test(form.password), /[^A-Za-z0-9]/.test(form.password)].filter(Boolean).length;
  const totalSteps = emailVerificationEnabled ? 4 : 3;
  const progressPercent = Math.round(step / totalSteps * 100);

  return (
    <div className="registration-page">
      <div className="registration-bg" /><div className="registration-grid" />
      <header className="registration-header"><AppLink className="registration-brand" to="/"><img src={logo} alt="SafeNet logo" /><span>SAFENET</span></AppLink><div className="system-status"><i /> REGISTRATION NODE: ONLINE</div><AppLink className="back-link" to="/">← MAIN WEBSITE</AppLink></header>
      <main className="registration-shell">
        <section className="mission-brief"><p className="kicker">STUDENT ACCESS // ENROLLMENT PROTOCOL</p><h1>Initialize your<br /><span>cyber profile.</span></h1><p className="intro">Create your SafeNet identity now. Join a class with a code, or let your professor enroll you later.</p>
          {view === 'register' && <button type="button" className="forgot-password-link" onClick={() => setView('forgot')}>Already registered? Forgot your password?</button>}
          {view === 'register' && <div className="protocol-list">{[['IDENTITY SCAN', 'Student information'], ['CLASS UPLINK', 'Enter a code or skip for now'], ['SECURE ACCESS', 'Create account password'], ...(emailVerificationEnabled ? [['EMAIL CHECK', verificationMode === 'link' ? 'Open the link sent to your email' : 'Verify your email address']] : [])].map(([title, text], index) => <article className={step === index + 1 ? 'active' : step > index + 1 ? 'complete' : ''} key={title}><b>0{index + 1}</b><div><span>{title}</span><small>{text}</small></div></article>)}</div>}
          <div className="security-note"><strong>SECURITY ADVISORY</strong><p>No class code yet? You can skip it and ask your professor to enroll your registered student number. Never share your password.</p></div>
        </section>
        {view === 'forgot' ? (
          <section className="registration-panel">
            <div className="reset-tabs"><button type="button" onClick={backToRegistration}>← BACK TO REGISTRATION</button></div>
            <div className="form-heading"><h2>Reset Your Password</h2></div>
            <form onSubmit={requestForgotPassword}>
              <div className="field-grid"><label className="full-field">School Email<input type="email" value={forgotEmail} onChange={(event) => setForgotEmail(event.target.value)} placeholder="student@school.edu" autoComplete="email" required /></label></div>
              {forgotNotice && <p className="otp-status" role="status">{forgotNotice}</p>}
              {forgotMessage && <div className="form-message" role="alert">{forgotMessage}</div>}
              <div className="form-actions">
                <button className="primary-button" style={{ width: '100%' }} disabled={loading}>{loading ? 'SENDING...' : 'SEND RESET LINK'}</button>
              </div>
            </form>
          </section>
        ) : (
        <section className="registration-panel">
          {created ? <RegistrationSuccess result={created} /> :
          <>
            <div className="panel-topline"><span>STEP 0{step} / 0{totalSteps}</span><div className="progress-track"><i className={`pct-${progressPercent}`} /></div><span>{progressPercent}%</span></div>
            <form onSubmit={(event) => event.preventDefault()}>
              {step === 1 && <div className="form-step active"><div className="form-heading"><p className="kicker">PROFILE CONFIGURATION</p><h2>Student Identity</h2><p>Enter your official school information.</p></div><div className="field-grid"><label className="full-field">Full Name<input name="fullname" value={form.fullname} onChange={update} autoComplete="name" placeholder="e.g. Andrea Santos" /></label><label>Student Number<input name="student_number" value={form.student_number} onChange={update} placeholder="2026-00124" /></label><label>Education Program<select name="program" value={form.program} onChange={update}><option value="">Select program</option>{educationPrograms.map(([code, name]) => <option value={code} key={code}>{code} — {name}</option>)}</select></label><label className="full-field">School Email<input name="email" type="email" value={form.email} onChange={update} placeholder="student@school.edu" autoComplete="email" /></label></div></div>}
              {step === 2 && (
                <div className="form-step active">
                  <div className="form-heading">
                    <p className="kicker">OPTIONAL CLASS CONNECTION</p>
                    <h2>Connect to Class</h2>
                    <p>Have a class code? Enter the six-character code from your professor.</p>
                  </div>
                  <label className="code-field">Class Code (optional)
                    <input name="class_code" value={form.class_code} onChange={update}
                      placeholder="ABC234" maxLength={6} disabled={loading}
                      aria-describedby="class-code-help" />
                  </label>
                  <div className={`code-status${verifiedClass ? ' verified' : ''}`}>
                    <span aria-hidden="true">{verifiedClass ? '✓' : '⌁'}</span>
                    <div>
                      <strong>{verifiedClass ? 'CLASS UPLINK VERIFIED' : 'NO CODE YET? THAT IS OKAY.'}</strong>
                      <p id="class-code-help">{verifiedClass
                        ? `${verifiedClass.section} • ${verifiedClass.subject}`
                        : 'Skip this step to create your account. Your professor can enroll you later using your student number.'}</p>
                    </div>
                  </div>
                  <button className="secondary-button skip-class-button" type="button"
                    disabled={loading} onClick={skipClass}>SKIP FOR NOW — ENROLL LATER</button>
                </div>
              )}
              {step === 3 && <div className="form-step active"><div className="form-heading"><p className="kicker">ACCOUNT SECURITY</p><h2>Secure Your Access</h2><p>Create a private password for your student profile.</p></div><div className="field-grid"><label>Password<PasswordInput name="password" value={form.password} onChange={update} placeholder="Minimum 8 characters" autoComplete="new-password" /></label><label>Confirm Password<PasswordInput name="confirmPassword" value={form.confirmPassword} onChange={update} placeholder="Repeat password" autoComplete="new-password" /></label></div><div className="strength-meter"><span className={`pct-${strength * 25}`} /></div><p className="strength-label">PASSWORD STRENGTH: {['NOT SET', 'WEAK', 'FAIR', 'GOOD', 'STRONG'][strength]}</p><label className="consent"><input name="consent" type="checkbox" checked={form.consent} onChange={update} /><span>I confirm that the information is accurate and for authorized educational activities.</span></label></div>}
              {emailVerificationEnabled && step === 4 && <div className="form-step active"><div className="form-heading"><p className="kicker">EMAIL VERIFICATION</p><h2>Verify Your Email</h2><p>{verificationMode === 'link' ? verificationSession ? <>A verification link was sent to <strong>{form.email}</strong>. Open it, then return here.</> : <>Request a verification link for <strong>{form.email}</strong>.</> : verificationSession ? <>Enter the six-digit code sent to <strong>{form.email}</strong>. The code expires in 10 minutes.</> : <>Request a six-digit code for <strong>{form.email}</strong>, then enter it below.</>}</p></div><button className="secondary-button resend-otp-button" type="button" disabled={loading} onClick={resendOtp}>{verificationSession ? `SEND A NEW ${verificationMode === 'link' ? 'LINK' : 'CODE'}` : `SEND VERIFICATION ${verificationMode === 'link' ? 'LINK' : 'CODE'}`}</button>{verificationMode === 'code' && <label className="code-field otp-code-field">Verification Code<input name="otp" value={form.otp} onChange={update} inputMode="numeric" autoComplete="one-time-code" placeholder="000000" maxLength={6} /></label>}{/* The heading above already says the link was sent, and the submit button below already reads CREATE VERIFIED PROFILE — a static "select Create Verified Profile below" reminder here duplicated both, which is what the usability survey flagged. */}{otpStatus && <p className="otp-status" role="status">{otpStatus}</p>}</div>}
              <RegistrationBotTrap value={botTrap} onChange={(event) => setBotTrap(event.target.value)} />
              {step === 3 && <p className="enrollment-summary">
                {verifiedClass ? `Joining ${verifiedClass.section} • ${verifiedClass.subject}`
                  : 'No class selected. Your account will await professor enrollment before you can sign in to the game.'}
              </p>}
              {message && <div className="form-message" role="alert">{message}</div>}
              <div className="form-actions">{step > 1 && <button className="secondary-button" type="button" disabled={loading} onClick={() => { setMessage(''); setOtpStatus(''); setStep((value) => value - 1); }}>← BACK</button>}<button className="primary-button" type="button" disabled={loading} onClick={next}>{loading ? 'PROCESSING...' : step === 4 ? verificationMode === 'link' ? 'CREATE VERIFIED PROFILE' : 'VERIFY & CREATE PROFILE' : step === 3 ? emailVerificationEnabled ? `EMAIL VERIFICATION ${verificationMode === 'link' ? 'LINK' : 'CODE'} →` : 'CREATE PROFILE →' : step === 2 ? 'VERIFY & CONTINUE →' : 'CONTINUE →'}</button></div>
            </form>
          </>}
        </section>
        )}
      </main>
      <footer>SAFENET CYBER MISSION INTERFACE • STUDENT ENROLLMENT SYSTEM</footer>
    </div>
  );
}

function RegistrationSuccess({ result }) {
  const enrolled = Boolean(result.class);
  return (
    <section className="success-state">
      <div className="success-icon" aria-hidden="true">✓</div>
      <p className="kicker">REGISTRATION COMPLETE</p>
      <h2>Profile initialized.</h2>
      <p role="status">{enrolled
        ? 'Your account is connected and ready for SafeNet training missions.'
        : 'Your account is created and awaiting professor enrollment.'}</p>
      <div className="credential-card">
        <span>STUDENT OPERATIVE</span>
        <strong>{result.student.fullname}</strong>
        <p>Student number: {result.student.student_number}</p>
        {enrolled ? <><p>{result.class.section}</p><code>{result.class.code}</code></>
          : <span className="pending-enrollment">AWAITING ENROLLMENT</span>}
      </div>
      {!enrolled && <p className="enrollment-summary">
        Give your professor your student number so they can add you from the
        Enrollment dashboard. Once enrolled, use their class code to sign in
        to the SafeNet game. You do not need to register again.
      </p>}
      <AppLink className="primary-button success-link" to="/">BACK TO SAFENET</AppLink>
    </section>
  );
}
