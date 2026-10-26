import { useEffect, useState } from 'react';
import HomePage from './pages/HomePage';
import ProfessorPortalPage from './pages/ProfessorPortalPage';
import StudentRegistrationPage from './pages/StudentRegistrationPage';
import EmailVerificationPage from './pages/EmailVerificationPage';
import ResetPasswordPage from './pages/ResetPasswordPage';

// Main website router: chooses which React page to display from the URL.
export default function App() {
  const [path, setPath] = useState(window.location.pathname);

  useEffect(() => {
    const updatePath = () => setPath(window.location.pathname);
    window.addEventListener('popstate', updatePath);
    return () => window.removeEventListener('popstate', updatePath);
  }, []);

  if (path === '/student/register') return <StudentRegistrationPage />;
  if (path === '/professor') return <ProfessorPortalPage />;
  if (path === '/verify-email') return <EmailVerificationPage />;
  if (path === '/reset-password') return <ResetPasswordPage />;
  return <HomePage />;
}
