import { useEffect, useMemo, useState } from 'react';
// Professor portal: login, classes, enrollment, performance, and reports.
import AppLink from '../components/AppLink';
import WeekdayPicker from '../components/WeekdayPicker';
import PortalNotification from '../components/PortalNotification';
import PasswordInput from '../components/PasswordInput';
import RegistrationBotTrap from '../components/RegistrationBotTrap';
import useEmailVerificationMode from '../hooks/useEmailVerificationMode';
import logo from '../assets/images/Logo.png';
import { apiRequest } from '../api/client';
import {
  PENDING_PROFESSOR_REGISTRATION_KEY,
  savePendingProfessorRegistration,
  clearPendingProfessorRegistration,
} from '../utils/pendingProfessorRegistration';
import '../styles/professor.css';

const views = [
  ['dashboard', '01', 'Dashboard'],
  ['classes', '02', 'Class Management'],
  ['codes', '03', 'Class Codes'],
  ['enrollment', '04', 'Enrollment'],
  ['points', '05', 'Points'],
  ['reports', '06', 'Reports'],
  ['help', '07', 'Help / About'],
];
const emptyClass = { subject: 'Living in the IT Era', section: '', meetingDays: [], startHour: '', startMinute: '00', startPeriod: 'AM', endHour: '', endMinute: '00', endPeriod: 'AM', termName: '', academicYear: '' };
const hours = Array.from({ length: 12 }, (_, index) => String(index + 1));
const minutes = ['00', '15', '30', '45'];
const academicYears = Array.from({ length: 6 }, (_, index) => `${2026 + index}-${2027 + index}`);
const emptyState = { professor: null, classes: [], students: [] };

function to24Hour(hour, minute, period) {
  // Converts the schedule selector from AM/PM into 24-hour time for the API.
  let value = Number(hour) % 12;
  if (period === 'PM') value += 12;
  return `${String(value).padStart(2, '0')}:${minute}`;
}

function completion(student) {
  // Calculates the percentage of missions completed by one student.
  return student.totalMissions ? Math.round((student.missionsCompleted / student.totalMissions) * 100) : 0;
}

function performance(student) {
  // Converts completion into the awareness label shown in reports.
  const value = completion(student);
  if (value >= 80) return ['High', 'performance-high'];
  if (value >= 50) return ['Developing', 'performance-mid'];
  return ['Needs Support', 'performance-low'];
}

function recordedDate(value) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

function AuthPortal({ onAuthenticated }) {
  // Shows professor registration and login before opening the dashboard.
  const verificationMode = useEmailVerificationMode();
  const emailVerificationEnabled = verificationMode !== 'disabled';
  const [mode, setMode] = useState('login');
  const [form, setForm] = useState({ fullname: '', email: '', password: '', otp: '' });
  const [verificationSession, setVerificationSession] = useState(null);
  const [message, setMessage] = useState('');
  const [notice, setNotice] = useState('');
  const [loading, setLoading] = useState(false);
  const [botTrap, setBotTrap] = useState('');
  const [forgotEmail, setForgotEmail] = useState('');

  function changeMode(nextMode) {
    setMode(nextMode);
    setVerificationSession(null);
    setMessage('');
    setNotice('');
    setForm({ fullname: '', email: '', password: '', otp: '' });
    setForgotEmail('');
  }

  async function requestForgotPassword(event) {
    event.preventDefault();
    setMessage(''); setNotice('');
    if (!/^\S+@\S+\.\S+$/.test(forgotEmail.trim())) {
      setMessage('Enter a valid email address.');
      return;
    }
    setLoading(true);
    try {
      const result = await apiRequest('/api/auth/professors/forgot-password', {
        method: 'POST',
        body: JSON.stringify({ email: forgotEmail.trim().toLowerCase() }),
      });
      setNotice(result.message);
    } catch (error) {
      setMessage(error.message);
    } finally {
      setLoading(false);
    }
  }

  function updateField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
    if (field === 'email') setVerificationSession(null);
    setMessage('');
    setNotice('');
  }

  async function requestVerification() {
    setMessage('');
    setNotice('');
    if (!/^\S+@\S+\.\S+$/.test(form.email.trim())) {
      setMessage(`Enter a valid email address before requesting a verification ${verificationMode === 'link' ? 'link' : 'code'}.`);
      return;
    }

    setLoading(true);
    try {
      const result = await apiRequest('/api/auth/professors/request-verification', {
        method: 'POST',
        body: JSON.stringify({ email: form.email.trim().toLowerCase() }),
      });
      if (verificationMode === 'link') {
        setVerificationSession({
          id: result.verificationId,
          proof: result.verificationProof,
        });
        // The email link may open in a different tab, so mirror what's
        // needed to finish registration to localStorage (shared across tabs
        // in this browser) instead of relying on this tab staying open.
        savePendingProfessorRegistration({
          email: form.email.trim().toLowerCase(),
          fullname: form.fullname.trim(),
          password: form.password,
          verification_id: result.verificationId,
          verification_proof: result.verificationProof,
        });
      } else {
        setVerificationSession({ codeRequested: true });
      }
      setNotice(result.message);
    } catch (error) {
      setVerificationSession(null);
      setMessage(error.message);
    } finally {
      setLoading(false);
    }
  }

  async function completeRegistration() {
    setLoading(true); setMessage('');
    try {
      await apiRequest('/api/auth/professors/register', {
        method: 'POST',
        body: JSON.stringify({
          fullname: form.fullname,
          email: form.email,
          password: form.password,
          website: botTrap,
          ...(verificationMode === 'code' ? { otp: form.otp } : {}),
          ...(verificationMode === 'link' && verificationSession ? {
            verification_id: verificationSession.id,
            verification_proof: verificationSession.proof,
          } : {}),
        }),
      });
      clearPendingProfessorRegistration();
      await onAuthenticated();
    } catch (error) {
      if (/email already exists/i.test(error.message) && verificationSession) {
        // Most likely means the email-link tab already finished registration;
        // log in with the same credentials so this tab is authenticated too.
        try {
          await apiRequest('/api/auth/professors/login', {
            method: 'POST',
            body: JSON.stringify({ email: form.email, password: form.password }),
          });
          clearPendingProfessorRegistration();
          await onAuthenticated();
        } catch {
          setMessage(error.message);
        }
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
      if (event.key !== PENDING_PROFESSOR_REGISTRATION_KEY || event.newValue) return;
      if (mode === 'register' && verificationMode === 'link' && verificationSession && !loading) {
        completeRegistration();
      }
    }
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  });

  async function submit(event) {
    event.preventDefault();
    if (mode === 'register') {
      await completeRegistration();
      return;
    }
    setLoading(true); setMessage('');
    try {
      await apiRequest('/api/auth/professors/login', {
        method: 'POST', body: JSON.stringify({ email: form.email, password: form.password }),
      });
      await onAuthenticated();
    } catch (error) { setMessage(error.message); } finally { setLoading(false); }
  }

  return (
    <main className="auth-shell">
      <section className="auth-copy">
        <p className="portal-kicker">FACULTY ACCESS NODE</p>
        <h1>Monitor the<br /><span>cyber mission.</span></h1>
        <p>Manage classes, issue access codes, monitor enrollment, and review student activity.</p>
      </section>
      <section className="auth-panel">
        <div className="auth-tabs">
          {mode === 'forgot' ? (
            <button className="active" onClick={() => changeMode('login')}>← BACK TO LOGIN</button>
          ) : (
            <>
              <button className={mode === 'login' ? 'active' : ''} onClick={() => changeMode('login')}>LOGIN</button>
              <button className={mode === 'register' ? 'active' : ''} onClick={() => changeMode('register')}>REGISTER</button>
            </>
          )}
        </div>
        {mode === 'forgot' ? (
          <form onSubmit={requestForgotPassword}>
            <h2>Reset Your Password</h2>
            <label>Email
              <input type="email" value={forgotEmail} onChange={(event) => setForgotEmail(event.target.value)} autoComplete="email" required />
            </label>
            {notice && <p className="portal-message success" role="status">{notice}</p>}
            {message && <p className="portal-message error" role="alert">{message}</p>}
            <button className="portal-primary" disabled={loading}>{loading ? 'SENDING...' : 'SEND RESET LINK'}</button>
          </form>
        ) : (
          <form onSubmit={submit}>
            {mode === 'register' && <RegistrationBotTrap value={botTrap} onChange={(event) => setBotTrap(event.target.value)} />}
            <h2>{mode === 'login' ? 'Professor Login' : 'Create Professor Account'}</h2>
            {mode === 'register' && (
              <label>Full Name
                <input value={form.fullname} onChange={(event) => updateField('fullname', event.target.value)} autoComplete="name" required />
              </label>
            )}
            <label>Email
              <input type="email" value={form.email} onChange={(event) => updateField('email', event.target.value)} autoComplete="email" required />
            </label>
            <label>Password
              <PasswordInput value={form.password} onChange={(event) => updateField('password', event.target.value)} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} minLength="6" required />
            </label>
            {mode === 'login' && (
              <button type="button" className="forgot-password-link" onClick={() => changeMode('forgot')}>Forgot password?</button>
            )}
            {mode === 'register' && emailVerificationEnabled && (
              <>
                <button className="secondary-button otp-send-button" type="button" disabled={loading} onClick={requestVerification}>
                  {verificationSession ? `SEND A NEW ${verificationMode === 'link' ? 'LINK' : 'CODE'}` : `SEND VERIFICATION ${verificationMode === 'link' ? 'LINK' : 'CODE'}`}
                </button>
                {verificationMode === 'code' && <label>Verification Code
                  <input className="otp-input" value={form.otp} onChange={(event) => updateField('otp', event.target.value.replace(/\D/g, '').slice(0, 6))} inputMode="numeric" autoComplete="one-time-code" maxLength="6" placeholder="000000" required />
                </label>}
              </>
            )}
            {/* The server's own notice message already covers the link-sent case
                ("A verification link was sent..."), and the submit button below
                already reads CREATE VERIFIED ACCOUNT — a second static banner
                here was redundant and is what the usability survey flagged. */}
            {notice && <p className="portal-message success" role="status">{notice}</p>}
            {message && <p className="portal-message error" role="alert">{message}</p>}
            <button className="portal-primary" disabled={loading}>
              {loading ? 'PROCESSING...' : mode === 'login' ? 'ACCESS PORTAL' : verificationMode === 'link' ? 'CREATE VERIFIED ACCOUNT' : emailVerificationEnabled ? 'VERIFY & CREATE ACCOUNT' : 'CREATE ACCOUNT'}
            </button>
          </form>
        )}
      </section>
    </main>
  );
}

export default function ProfessorPortalPage() {
  // Holds the selected dashboard view and all professor, class, and student data.
  const [data, setData] = useState(emptyState);
  const [activeView, setActiveView] = useState('dashboard');
  const [loading, setLoading] = useState(false);
  const [classForm, setClassForm] = useState(emptyClass);
  const [selectedClass, setSelectedClass] = useState('all');
  const [search, setSearch] = useState('');
  const [enrollForm, setEnrollForm] = useState({ studentNumber: '', classCode: '' });
  const [pointsStudent, setPointsStudent] = useState(null);
  const [pointsForm, setPointsForm] = useState({ points: 0, missionsCompleted: 0 });
  const [historyStudent, setHistoryStudent] = useState(null);
  const [historyData, setHistoryData] = useState(null);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [lastSynced, setLastSynced] = useState(null);
  const [portalNotice, setPortalNotice] = useState(null);

  async function loadDashboard() {
    const result = await apiRequest('/api/professor/dashboard');
    setData({ professor: result.professor, classes: result.professor.classes, students: result.professor.students });
    setLastSynced(new Date());
  }

  useEffect(() => {
    document.title = 'SafeNet | Professor Portal';
    // Restore an existing session in the background so the login form is never
    // blocked by a sleeping free-tier API service.
    loadDashboard().catch(() => {});
  }, []);

  useEffect(() => {
    // Pull newly synchronized Unity results during a live class presentation.
    if (!data.professor) return undefined;
    const timer = window.setInterval(() => loadDashboard().catch(() => {}), 10000);
    return () => window.clearInterval(timer);
  }, [data.professor]);

  const filteredStudents = useMemo(() => data.students.filter((student) => {
    const inClass = selectedClass === 'all' || student.classId === selectedClass;
    const query = search.toLowerCase();
    return inClass && (!query || student.name.toLowerCase().includes(query) || student.studentNumber.toLowerCase().includes(query));
  }), [data.students, selectedClass, search]);

  const selectedCodeClass = data.classes.find((item) => item.id === selectedClass) || data.classes[0];
  const averagePoints = data.students.length ? Math.round(data.students.reduce((sum, item) => sum + item.points, 0) / data.students.length) : 0;
  const totalMissions = data.students.reduce((sum, item) => sum + item.missionsCompleted, 0);
  const updateClass = (event) => setClassForm((value) => ({ ...value, [event.target.name]: event.target.value }));
  const toggleDay = (day) => setClassForm((value) => ({ ...value, meetingDays: value.meetingDays.includes(day) ? value.meetingDays.filter((item) => item !== day) : [...value.meetingDays, day] }));

  async function runAction(action, successMessage) {
    setLoading(true);
    setPortalNotice(null);
    try {
      await action();
      await loadDashboard();
      setPortalNotice({ kind: 'success', title: 'Update completed', message: successMessage });
      return true;
    } catch (error) {
      setPortalNotice({ kind: 'error', title: 'Update could not be completed', message: error.message });
      return false;
    } finally {
      setLoading(false);
    }
  }

  async function refreshScores() {
    setPortalNotice(null);
    try {
      await loadDashboard();
      setPortalNotice({ kind: 'success', title: 'Scores refreshed', message: 'The latest Unity results are now displayed.' });
    } catch (error) {
      setPortalNotice({ kind: 'error', title: 'Scores could not be refreshed', message: error.message });
    }
  }

  async function createClass(event) {
    event.preventDefault();
    if (loading) return;
    setLoading(true);
    setPortalNotice(null);
    const days = classForm.meetingDays.map((day) => day.slice(0, 3)).join(', ');
    try {
      const result = await apiRequest('/api/classes', {
        method: 'POST',
        body: JSON.stringify({
          subject: classForm.subject, section: classForm.section, meetingDays: classForm.meetingDays,
          startTime: to24Hour(classForm.startHour, classForm.startMinute, classForm.startPeriod),
          endTime: to24Hour(classForm.endHour, classForm.endMinute, classForm.endPeriod),
          schedule: `${days} ${classForm.startHour}:${classForm.startMinute} ${classForm.startPeriod}-${classForm.endHour}:${classForm.endMinute} ${classForm.endPeriod}`,
          term: `${classForm.termName} ${classForm.academicYear}`,
        }),
      });
      // Show confirmation immediately, even when the form is below the fold.
      setPortalNotice({
        kind: 'success',
        title: 'Class created successfully',
        message: `${result.class.section} • ${result.class.subject}`,
        code: result.class.code,
        schedule: result.class.schedule,
      });
      setData((current) => ({
        ...current,
        classes: [result.class, ...current.classes.filter((item) => item.id !== result.class.id)],
      }));
      setClassForm(emptyClass);
      // A failed refresh must not claim that a successfully saved class failed.
      try { await loadDashboard(); } catch {
        setPortalNotice((notice) => notice ? ({
          ...notice,
          message: `${notice.message}. The class was saved, but the dashboard could not refresh.`,
        }) : ({
          kind: 'success',
          title: 'Class created successfully',
          message: 'The class was saved, but the dashboard could not refresh.',
        }));
      }
    } catch (error) {
      setPortalNotice({ kind: 'error', title: 'Class could not be created', message: error.message });
      // Keep the entered schedule so the professor can correct it and retry.
    } finally {
      setLoading(false);
    }
  }

  async function logout() {
    await apiRequest('/api/auth/professors/logout', { method: 'POST' }).catch(() => {});
    setData(emptyState);
  }

  async function enroll(event) {
    event.preventDefault();
    if (loading) return;
    const succeeded = await runAction(() => apiRequest('/api/professor/dashboard/students/enroll', { method: 'POST', body: JSON.stringify(enrollForm) }), 'Student enrollment updated.');
    if (succeeded) setEnrollForm({ studentNumber: '', classCode: '' });
  }

  function openPoints(student) {
    setPointsStudent(student);
    setPointsForm({ points: student.points, missionsCompleted: student.missionsCompleted });
  }

  async function openHistory(student) {
    setHistoryStudent(student);
    setHistoryData(null);
    setHistoryLoading(true);
    try {
      const result = await apiRequest(`/api/professor/dashboard/students/${student.id}/score-history`);
      setHistoryData(result);
    } catch (error) {
      setHistoryData({ error: error.message, history: [] });
    } finally {
      setHistoryLoading(false);
    }
  }

  async function savePoints(event) {
    event.preventDefault();
    await runAction(() => apiRequest(`/api/professor/dashboard/students/${pointsStudent.id}/points`, { method: 'PATCH', body: JSON.stringify({ points: Number(pointsForm.points), missionsCompleted: Number(pointsForm.missionsCompleted) }) }), 'Student performance updated.');
    setPointsStudent(null);
  }

  function exportCsv() {
    const rows = [['Student', 'Student Number', 'Class', 'Points', 'Missions Completed', 'Total Missions'], ...filteredStudents.map((student) => [student.name, student.studentNumber, data.classes.find((item) => item.id === student.classId)?.section || '', student.points, student.missionsCompleted, student.totalMissions])];
    const csv = rows.map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(',')).join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'safenet-student-report.csv'; anchor.click(); URL.revokeObjectURL(url);
  }

  return <div className="professor-page">
    <PortalNotification notice={portalNotice} onDismiss={() => setPortalNotice(null)} />
    <header className="professor-header"><AppLink to="/" className="professor-brand"><img src={logo} alt="SafeNet logo" /><span>SAFENET</span></AppLink><span>PROFESSOR COMMAND PORTAL</span>{data.professor ? <button type="button" onClick={logout}>LOG OUT</button> : <AppLink to="/">MAIN WEBSITE</AppLink>}</header>
    {!data.professor ? <AuthPortal onAuthenticated={loadDashboard} /> : <div className="workspace">
      <aside className={`portal-sidebar${sidebarOpen ? ' open' : ''}`}><div className="professor-chip"><div className="avatar">{data.professor.name.split(' ').map((part) => part[0]).slice(0, 2).join('')}</div><div><strong>{data.professor.name}</strong><span>{data.professor.email}</span></div></div><p className="sidebar-label">COMMAND MODULES</p>{views.map(([id, number, label]) => <button className={activeView === id ? 'active' : ''} onClick={() => { setActiveView(id); setSidebarOpen(false); }} key={id}><span>{number}</span>{label}</button>)}</aside>
      <main className="portal-workspace"><div className="workspace-title"><button className="sidebar-toggle" onClick={() => setSidebarOpen((value) => !value)} aria-expanded={sidebarOpen} aria-label={`${sidebarOpen ? 'Close' : 'Open'} professor dashboard menu`}>☰</button><div><p className="portal-kicker">SAFENET PROFESSOR SYSTEM</p><h2>{views.find(([id]) => id === activeView)?.[2]}</h2></div></div>
        {activeView === 'dashboard' && <section className="portal-view"><div className="welcome-panel"><div><p className="portal-kicker">SYSTEM OVERVIEW</p><h3>Welcome back, {data.professor.name.split(' ')[0]}.</h3><p>Review your SafeNet classes and student training activity.</p><small>{lastSynced ? `Scores synchronized ${lastSynced.toLocaleTimeString()}` : 'Waiting for score synchronization'}</small></div><button className="secondary-button" type="button" onClick={refreshScores}>REFRESH SCORES</button></div><div className="stat-grid"><article><span>ACTIVE CLASSES</span><strong>{data.classes.length}</strong><small>Managed sections</small></article><article><span>ENROLLED STUDENTS</span><strong>{data.students.length}</strong><small>Active accounts</small></article><article><span>AVERAGE POINTS</span><strong>{averagePoints}</strong><small>Across students</small></article><article><span>MISSIONS COMPLETED</span><strong>{totalMissions}</strong><small>Recorded completions</small></article></div><div className="dashboard-grid"><section className="content-panel"><div className="panel-heading"><div><p className="portal-kicker">RECENT CLASSES</p><h3>Class Overview</h3></div></div>{data.classes.slice(0, 4).map((item) => <div className="dashboard-row" key={item.id}><div><strong>{item.section}</strong><span>{item.subject}</span></div><code>{item.code}</code></div>)}</section><section className="content-panel"><div className="panel-heading"><div><p className="portal-kicker">STUDENT ACTIVITY</p><h3>Live Unity Results</h3></div></div>{data.students.slice(0, 5).map((student) => <div className="dashboard-row" key={student.id}><div><strong>{student.name}</strong><span>{student.missionsCompleted}/{student.totalMissions} missions</span></div><b>{student.points} pts</b></div>)}</section></div></section>}
        {activeView === 'classes' && <section className="portal-view two-column-layout"><article className="content-panel sticky-panel"><div className="panel-heading"><div><p className="portal-kicker">NEW CLASS</p><h3>Create a Class</h3></div></div><form className="class-form" onSubmit={createClass}><label>Course / Subject<input name="subject" value={classForm.subject} onChange={updateClass} required /></label><label>Section Name<input name="section" value={classForm.section} onChange={updateClass} placeholder="Example: BSIT 3A" required /></label><fieldset className="schedule-builder"><legend>Meeting Schedule</legend><p>Select every day this class meets.</p><WeekdayPicker selectedDays={classForm.meetingDays} onToggle={toggleDay} disabled={loading} /><div className="time-grid"><TimeSelector prefix="start" title="Start Time" form={classForm} update={updateClass} /><TimeSelector prefix="end" title="End Time" form={classForm} update={updateClass} /></div><div className="schedule-preview"><span>SCHEDULE PREVIEW</span><strong>{classForm.meetingDays.length && classForm.startHour && classForm.endHour ? `${classForm.meetingDays.map((day) => day.slice(0, 3)).join(', ')} • ${classForm.startHour}:${classForm.startMinute} ${classForm.startPeriod}-${classForm.endHour}:${classForm.endMinute} ${classForm.endPeriod}` : 'Select meeting days and times'}</strong></div></fieldset><div className="term-grid"><label>Term<select name="termName" value={classForm.termName} onChange={updateClass} required><option value="">Select term</option><option>1st Term</option><option>2nd Term</option></select></label><label>Academic Year<select name="academicYear" value={classForm.academicYear} onChange={updateClass} required><option value="">Select academic year</option>{academicYears.map((value) => <option key={value}>{value}</option>)}</select></label></div><button className="portal-primary" disabled={loading}>CREATE CLASS &amp; CODE</button></form></article><div><div className="section-action-row"><div><p className="portal-kicker">CLASS MANAGEMENT</p><h3>Managed Classes</h3></div><span>{data.classes.length} classes</span></div><div className="class-card-grid">{data.classes.map((item) => <article className="managed-class-card" key={item.id}><h4>{item.section}</h4><p>{item.subject}</p><div className="class-meta"><span>{item.schedule}</span><span>{item.term}</span></div><div className="class-code-row"><code>{item.code}</code><button className="danger-button" onClick={() => runAction(() => apiRequest(`/api/classes/${item.id}`, { method: 'DELETE' }), 'Class deactivated successfully.')}>DELETE</button></div></article>)}</div></div></section>}
        {activeView === 'codes' && <section className="portal-view"><article className="content-panel code-generator-panel"><p className="portal-kicker">CLASS CODE GENERATION</p><h3>Student Registration Code</h3><label className="select-label">Select Class<select value={selectedCodeClass?.id || ''} onChange={(event) => setSelectedClass(event.target.value)}>{data.classes.map((item) => <option value={item.id} key={item.id}>{item.section}</option>)}</select></label><div className="code-display"><span>ACTIVE CLASS CODE</span><strong>{selectedCodeClass?.code || '------'}</strong><small>{selectedCodeClass?.section || 'Create a class first'}</small></div><div className="code-actions"><button className="portal-primary" onClick={() => navigator.clipboard.writeText(selectedCodeClass?.code || '')}>COPY CODE</button><button className="danger-button" disabled={!selectedCodeClass} onClick={() => runAction(() => apiRequest(`/api/classes/${selectedCodeClass.id}/regenerate-code`, { method: 'POST' }), 'Class code regenerated successfully.')}>REGENERATE CODE</button></div></article></section>}
        {activeView === 'enrollment' && <section className="portal-view"><StudentControls classes={data.classes} selected={selectedClass} setSelected={setSelectedClass} search={search} setSearch={setSearch} /><StudentTable students={filteredStudents} classes={data.classes} actionLabel="REMOVE" onAction={(student) => runAction(() => apiRequest(`/api/professor/dashboard/students/${student.id}`, { method: 'DELETE' }), 'Student removed successfully.')} /><article className="content-panel enrollment-form"><p className="portal-kicker">DATABASE ENROLLMENT</p><h3>Add a Registered Student</h3><p>Students can register without a class code. Enter their student number and one of your class codes to enroll them; their game scores are kept.</p><form onSubmit={enroll}><label>Student Number<input value={enrollForm.studentNumber} onChange={(event) => setEnrollForm({ ...enrollForm, studentNumber: event.target.value })} required /></label><label>Class Code<input value={enrollForm.classCode} onChange={(event) => setEnrollForm({ ...enrollForm, classCode: event.target.value.toUpperCase() })} maxLength="6" required /></label><button className="portal-primary" disabled={loading}>ENROLL STUDENT</button></form></article></section>}
        {activeView === 'points' && <section className="portal-view"><div className="points-history-guide"><div><p className="portal-kicker">GAME RUNS</p><h3>Edit totals or review progress by session</h3></div><span>VIEW HISTORY shows one dated total for each complete game login/run.</span></div><StudentControls classes={data.classes} selected={selectedClass} setSelected={setSelectedClass} search={search} setSearch={setSearch} /><StudentTable students={filteredStudents} classes={data.classes} actionLabel="EDIT" onAction={openPoints} onHistory={openHistory} showPerformance /></section>}
        {activeView === 'reports' && <section className="portal-view"><StudentControls classes={data.classes} selected={selectedClass} setSelected={setSelectedClass} search={search} setSearch={setSearch} /><div className="report-metrics"><article><span>STUDENTS</span><strong>{filteredStudents.length}</strong></article><article><span>AVERAGE POINTS</span><strong>{filteredStudents.length ? Math.round(filteredStudents.reduce((sum, item) => sum + item.points, 0) / filteredStudents.length) : 0}</strong></article><article><span>COMPLETION</span><strong>{filteredStudents.length ? Math.round(filteredStudents.reduce((sum, item) => sum + completion(item), 0) / filteredStudents.length) : 0}%</strong></article></div><StudentTable students={filteredStudents} classes={data.classes} showPerformance /><div className="report-actions"><button className="portal-primary" onClick={exportCsv}>EXPORT CSV</button><button className="secondary-button" onClick={() => window.print()}>PRINT REPORT</button></div></section>}
        {activeView === 'help' && <section className="portal-view help-grid">{[['Authentication', 'Register, log in, restore a session, and log out securely.'], ['Class Management', 'Create class schedules and generate student registration codes.'], ['Enrollment', 'Review registered students or transfer them using a valid class code.'], ['Points', 'Unity results synchronize automatically; professors can add a separate manual adjustment when needed.'], ['Reports', 'Filter class records, export CSV summaries, or print the current report.'], ['About SafeNet', 'SafeNet supports cybersecurity awareness training for college students.']].map(([title, text], index) => <article className="content-panel help-item" key={title}><span>0{index + 1}</span><div><h4>{title}</h4><p>{text}</p></div></article>)}</section>}
      </main>
    </div>}
    {pointsStudent && <div className="modal-backdrop"><form className="modal-card" onSubmit={savePoints}><button className="modal-close" type="button" onClick={() => setPointsStudent(null)}>×</button><p className="portal-kicker">PERFORMANCE EDITOR</p><h3>{pointsStudent.name}</h3><label>Total Points<input type="number" min="0" value={pointsForm.points} onChange={(event) => setPointsForm({ ...pointsForm, points: event.target.value })} required /></label><label>Missions Completed<input type="number" min="0" max="6" value={pointsForm.missionsCompleted} onChange={(event) => setPointsForm({ ...pointsForm, missionsCompleted: event.target.value })} required /></label><button className="portal-primary">SAVE PERFORMANCE</button></form></div>}
    {historyStudent && <div className="modal-backdrop"><section className="modal-card history-modal"><button className="modal-close" type="button" onClick={() => setHistoryStudent(null)}>×</button><div><p className="portal-kicker">RUN HISTORY</p><h3>{historyStudent.name}</h3></div>{historyLoading && <p className="history-status">Loading game runs...</p>}{historyData?.error && <p className="portal-message error">{historyData.error}</p>}{historyData && !historyData.error && <><div className="history-summary"><article><span>HIGHEST SAVED TOTAL</span><strong>{historyData.student.currentPoints}/{historyData.student.maximumScore}</strong></article><article><span>RECORDED RUNS</span><strong>{historyData.history.length}</strong></article></div>{historyData.history.length ? <div className="run-history-list">{historyData.history.map((run) => <article key={run.id}><div><strong>{run.score}/{run.maximumScore}</strong><span>{recordedDate(run.startedAt)}</span></div><small>{run.missionsCompleted}/{run.totalMissions} missions • {run.completedAt ? 'Completed run' : 'Progress saved'}</small></article>)}</div> : <p className="history-status">No game runs recorded yet. A run appears after the student signs in to Unity and saves mission progress.</p>}</>}</section></div>}
  </div>;
}

function TimeSelector({ prefix, title, form, update }) {
  // Reusable hour, minute, and AM/PM fields for class schedules.
  return <fieldset className="time-selector"><legend>{title}</legend><label>Hour<select name={`${prefix}Hour`} value={form[`${prefix}Hour`]} onChange={update} required><option value="">Hour</option>{hours.map((value) => <option key={value}>{value}</option>)}</select></label><label>Minute<select name={`${prefix}Minute`} value={form[`${prefix}Minute`]} onChange={update}>{minutes.map((value) => <option key={value}>{value}</option>)}</select></label><label>Period<select name={`${prefix}Period`} value={form[`${prefix}Period`]} onChange={update}><option>AM</option><option>PM</option></select></label></fieldset>;
}

function StudentControls({ classes, selected, setSelected, search, setSearch }) {
  // Provides class filtering and student searching.
  return <div className="section-action-row"><div><p className="portal-kicker">DATABASE RECORDS</p><h3>Registered Students</h3></div><div className="inline-controls"><select value={selected} onChange={(event) => setSelected(event.target.value)}><option value="all">All Classes</option>{classes.map((item) => <option value={item.id} key={item.id}>{item.section}</option>)}</select><input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search student..." /></div></div>;
}

function StudentTable({ students, classes, actionLabel, onAction, onHistory, showPerformance }) {
  // Shared student table used by enrollment, points, and report views.
  return <div className={`content-panel table-wrap${onHistory ? ' score-history-table' : ''}`}><table><thead><tr><th>Student</th><th>Student No.</th><th>Section</th><th>Points</th><th>Missions</th>{showPerformance && <th>Performance</th>}{(actionLabel || onHistory) && <th>Actions</th>}</tr></thead><tbody>{students.length ? students.map((student) => { const result = performance(student); return <tr key={student.id}><td><strong>{student.name}</strong><small>{student.email}</small></td><td>{student.studentNumber}</td><td>{classes.find((item) => item.id === student.classId)?.section}</td><td>{student.points}</td><td>{student.missionsCompleted}/{student.totalMissions}</td>{showPerformance && <td className={result[1]}>{result[0]}</td>}{(actionLabel || onHistory) && <td><div className="table-actions">{actionLabel && <button className={actionLabel === 'REMOVE' ? 'danger-button' : 'secondary-button'} onClick={() => onAction(student)}>{actionLabel}</button>}{onHistory && <button className="secondary-button" onClick={() => onHistory(student)}>VIEW HISTORY</button>}</div></td>}</tr>; }) : <tr><td colSpan="7" className="empty-table">No student records found.</td></tr>}</tbody></table></div>;
}
