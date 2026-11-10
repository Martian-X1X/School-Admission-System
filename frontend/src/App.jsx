import { useCallback, useEffect, useState } from 'react';
import {
  applyToClass,
  cancelHold,
  confirmHold,
  createClass,
  listClasses,
  listMyHolds,
} from './api.js';
import { usePolling, useNow } from './hooks.js';
import { errorMessage, isValidStudentId } from './utils.js';
import StudentBar from './components/StudentBar.jsx';
import AdminPanel from './components/AdminPanel.jsx';
import ClassList from './components/ClassList.jsx';
import MyApplications from './components/MyApplications.jsx';

const POLL_MS = 3000;

export default function App() {
  const [studentId, setStudentId] = useState(() => {
    try {
      return localStorage.getItem('studentId') || '';
    } catch {
      return '';
    }
  });

  const [tab, setTab] = useState('classes');
  const [notice, setNotice] = useState(null);
  const [busyId, setBusyId] = useState(null);

  const studentValid = isValidStudentId(studentId);
  const now = useNow();

  useEffect(() => {
    try {
      localStorage.setItem('studentId', studentId);
    } catch {
      /* Private mode: ignore */
    }
  }, [studentId]);

  const classes = usePolling(listClasses, POLL_MS, 'classes', true);
  const mine = usePolling(() => listMyHolds(studentId), POLL_MS, studentId, studentValid);

  const refreshAll = useCallback(() => {
    classes.refresh();
    mine.refresh();
  }, [classes.refresh, mine.refresh]);

  async function run(id, action, successText) {
    setBusyId(id);

    try {
      await action();
      setNotice({ type: 'ok', text: successText });
      return true;
    } catch (err) {
      setNotice({ type: 'error', text: errorMessage(err) });
      return false;
    } finally {
      setBusyId(null);
      refreshAll();
    }
  }

  const onCreate = (name, seats) =>
    run('create', () => createClass(name, seats), 'Class created successfully.');

  const onApply = (classId) =>
    run(
      classId,
      () => applyToClass(classId, studentId),
      'Application submitted successfully. Please pay the admission fee before the hold expires.'
    );

  const onConfirm = (holdId) =>
    run(
      holdId,
      () => confirmHold(holdId, studentId),
      'Admission fee submitted successfully. Your admission is confirmed!'
    );

  const onCancel = (holdId) => {
    if (!window.confirm('Are you sure you want to cancel this application?')) {
      return Promise.resolve(false);
    }

    return run(
      holdId,
      () => cancelHold(holdId, studentId),
      'Application cancelled successfully.'
    );
  };

  const serverDown =
    classes.error?.code === 'NETWORK' || mine.error?.code === 'NETWORK';

  return (
    <main className="container">
      <h1>School Online Admission</h1>

      <p className="muted">
        First come, first served — admission policy.
      </p>

      <StudentBar value={studentId} onChange={setStudentId} />

      {serverDown && (
        <div className="banner error">
          {errorMessage({ code: 'NETWORK' })}
        </div>
      )}

      {notice && (
        <div className={`banner ${notice.type}`} role="status">
          <span>{notice.text}</span>
          <button
            className="link"
            onClick={() => setNotice(null)}
            aria-label="Close notification"
          >
            ✕
          </button>
        </div>
      )}

      <nav className="tabs">
        <button
          className={tab === 'classes' ? 'active' : ''}
          onClick={() => setTab('classes')}
        >
          Class List
        </button>

        <button
          className={tab === 'mine' ? 'active' : ''}
          onClick={() => setTab('mine')}
        >
          My Applications
        </button>

        <button
          className={tab === 'admin' ? 'active' : ''}
          onClick={() => setTab('admin')}
        >
          Admin
        </button>
      </nav>

      {tab === 'classes' && (
        <ClassList
          classes={classes.data}
          loading={classes.loading}
          myHolds={mine.data}
          studentValid={studentValid}
          now={now}
          busyId={busyId}
          onApply={onApply}
        />
      )}

      {tab === 'mine' && (
        <MyApplications
          holds={mine.data}
          loading={mine.loading}
          studentValid={studentValid}
          now={now}
          busyId={busyId}
          onConfirm={onConfirm}
          onCancel={onCancel}
          onExpired={refreshAll}
        />
      )}

      {tab === 'admin' && (
        <AdminPanel
          onCreate={onCreate}
          busy={busyId === 'create'}
        />
      )}
    </main>
  );
}