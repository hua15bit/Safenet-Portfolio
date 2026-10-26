import { useEffect, useRef } from 'react';

const AUTO_DISMISS_MS = 8000; // Matches the .toast-progress animation duration in professor.css.

export default function PortalNotification({ notice, onDismiss, duration = AUTO_DISMISS_MS }) {
  // Keep the live region mounted so assistive technology announces new results.
  // Every new dashboard result gets a fresh timer and can still be closed early.
  const dismissRef = useRef(onDismiss);

  useEffect(() => {
    dismissRef.current = onDismiss;
  }, [onDismiss]);

  useEffect(() => {
    if (!notice) return undefined;
    const timer = window.setTimeout(() => dismissRef.current?.(), duration);
    return () => window.clearTimeout(timer);
  }, [duration, notice]);

  return (
    <div className="portal-notifications" aria-live="polite" aria-atomic="true">
      {notice && (
        <section className={`portal-toast ${notice.kind}`} role="status">
          <span className="toast-icon" aria-hidden="true">{notice.kind === 'success' ? '✓' : '!'}</span>
          <div className="toast-content">
            <h3>{notice.title}</h3>
            <p>{notice.message}</p>
            {notice.schedule && <p>{notice.schedule}</p>}
            {notice.code && <p className="toast-code">Class code: <strong>{notice.code}</strong></p>}
          </div>
          <button className="toast-dismiss" type="button" onClick={onDismiss}
            aria-label="Dismiss notification">×</button>
          <span className="toast-progress" aria-hidden="true" />
        </section>
      )}
    </div>
  );
}
