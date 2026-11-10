
import { useCallback, useEffect, useState } from 'react';
import {
  BrowserRouter,
  Routes,
  Route,
  NavLink,
  Navigate,
  Link,
} from 'react-router-dom';

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

/* Shared site header */

function SiteHeader() {
  return (
    <header className="site-header">
      <div className="header-inner">
        <Link to="/" className="brand">
          <span className="brand-mark">SA</span>
          <span>School Admission Portal (স্কুল ভর্তি পোর্টাল)</span>
        </Link>

        <nav className="header-nav" aria-label="Main navigation">
          <NavLink to="/" end>
            Student Portal (শিক্ষার্থী পোর্টাল)
          </NavLink>

          <NavLink to="/admin">
            Administration (প্রশাসন)
          </NavLink>
        </nav>
      </div>
    </header>
  );
}

/* Shared notification */

function Notice({ notice, onClose }) {
  if (!notice) return null;

  return (
    <div className={`banner ${notice.type}`} role="status">
      <span>{notice.text}</span>

      <button
        type="button"
        className="link"
        onClick={onClose}
        aria-label="Close notification"
      >
        ✕
      </button>
    </div>
  );
}

/* Student Portal */

function StudentPortal() {
  const [studentId, setStudentId] = useState(() => {
    try {
      return localStorage.getItem('studentId') || '';
    } catch {
      return '';
    }
  });

  const [notice, setNotice] = useState(null);
  const [busyId, setBusyId] = useState(null);

  const studentValid = isValidStudentId(studentId);
  const now = useNow();

  useEffect(() => {
    try {
      localStorage.setItem('studentId', studentId);
    } catch {
      // Ignore storage restrictions, such as private browsing.
    }
  }, [studentId]);

  const classes = usePolling(
    listClasses,
    POLL_MS,
    'classes',
    true
  );

  const mine = usePolling(
    () => listMyHolds(studentId),
    POLL_MS,
    studentId,
    studentValid
  );

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
      setNotice({
        type: 'error',
        text: errorMessage(err),
      });
      return false;
    } finally {
      setBusyId(null);
      refreshAll();
    }
  }

  const onApply = (classId) =>
    run(
      classId,
      () => applyToClass(classId, studentId),
      'Your application has been submitted successfully. (আপনার আবেদন সফলভাবে জমা হয়েছে।)'
    );

  const onConfirm = (holdId) =>
    run(
      holdId,
      () => confirmHold(holdId, studentId),
      'Your admission has been confirmed successfully. (আপনার ভর্তি সফলভাবে নিশ্চিত হয়েছে।)'
    );

  const onCancel = (holdId) => {
    if (
      !window.confirm(
        'Are you sure you want to cancel this application? (আপনি কি নিশ্চিত যে এই আবেদনটি বাতিল করতে চান?)'
      )
    ) {
      return Promise.resolve(false);
    }

    return run(
      holdId,
      () => cancelHold(holdId, studentId),
      'Your application has been cancelled successfully. (আপনার আবেদন সফলভাবে বাতিল হয়েছে।)'
    );
  };

  const serverDown =
    classes.error?.code === 'NETWORK' ||
    (studentValid && mine.error?.code === 'NETWORK');

  return (
    <>
      <SiteHeader />

      <main className="container">
        <section className="page-header">
          <div>
            <h1>Student Admission Portal (শিক্ষার্থী ভর্তি পোর্টাল)</h1>
            <p className="page-description">
              Browse available classes and manage your admission applications.
              (উপলব্ধ শ্রেণিগুলো দেখুন এবং আপনার ভর্তি আবেদনগুলো পরিচালনা
              করুন।)
            </p>
          </div>
        </section>

        <StudentBar
          value={studentId}
          onChange={setStudentId}
        />

        {serverDown && (
          <div className="banner error" role="alert">
            {errorMessage({ code: 'NETWORK' })}
          </div>
        )}

        <Notice
          notice={notice}
          onClose={() => setNotice(null)}
        />

        <nav className="tabs" aria-label="Student sections">
          <a href="#available-classes">Available Classes (উপলব্ধ শ্রেণি)</a>
          <a href="#my-applications">My Applications (আমার আবেদন)</a>
        </nav>

        <section id="available-classes">
          <div className="panel-header">
            <div>
              <h2>Available Classes (উপলব্ধ শ্রেণি)</h2>
              <p className="muted">
                Choose a class with available seats to apply. (উপলব্ধ আসন আছে
                এমন একটি শ্রেণিতে আবেদন করুন।)
              </p>
            </div>
          </div>

          <ClassList
            classes={classes.data || []}
            loading={classes.loading}
            myHolds={mine.data || []}
            studentValid={studentValid}
            now={now}
            busyId={busyId}
            onApply={onApply}
          />
        </section>

        <section id="my-applications">
          <div className="panel-header">
            <div>
              <h2>My Applications (আমার আবেদন)</h2>
              <p className="muted">
                Track your seat holds and admission status. (আপনার আসন
                সংরক্ষণ ও ভর্তির অবস্থা অনুসরণ করুন।)
              </p>
            </div>

            {studentValid && (
              <button
                type="button"
                className="secondary"
                onClick={refreshAll}
              >
                Refresh (রিফ্রেশ)
              </button>
            )}
          </div>

          <MyApplications
            holds={mine.data || []}
            loading={mine.loading}
            studentValid={studentValid}
            now={now}
            busyId={busyId}
            onConfirm={onConfirm}
            onCancel={onCancel}
            onExpired={refreshAll}
          />
        </section>

        <footer className="site-footer">
          School Admission Portal · Student Services (স্কুল ভর্তি পোর্টাল ·
          শিক্ষার্থী সেবা)
        </footer>
      </main>
    </>
  );
}

/* Admin Dashboard */

function AdminDashboard() {
  const [notice, setNotice] = useState(null);
  const [busy, setBusy] = useState(false);

  const classes = usePolling(
    listClasses,
    POLL_MS,
    'admin-classes',
    true
  );

  const refreshClasses = useCallback(() => {
    classes.refresh();
  }, [classes.refresh]);

  async function onCreate(name, seats) {
    setBusy(true);

    try {
      await createClass(name, seats);

      setNotice({
        type: 'ok',
        text: 'Class created successfully. (শ্রেণি সফলভাবে তৈরি হয়েছে।)',
      });

      refreshClasses();
      return true;
    } catch (err) {
      setNotice({
        type: 'error',
        text: errorMessage(err),
      });

      return false;
    } finally {
      setBusy(false);
    }
  }

  const classItems = Array.isArray(classes.data)
    ? classes.data
    : [];

  const totalClasses = classItems.length;

  const totalSeats = classItems.reduce(
    (sum, item) => sum + Number(item.totalSeats || 0),
    0
  );

  const heldSeats = classItems.reduce(
    (sum, item) => sum + Number(item.heldSeats || 0),
    0
  );

  const confirmedSeats = classItems.reduce(
    (sum, item) => sum + Number(item.confirmedSeats || 0),
    0
  );

  const availableSeats = classItems.reduce(
    (sum, item) => sum + Number(item.availableSeats || 0),
    0
  );

  return (
    <>
      <SiteHeader />

      <main className="container">
        <section className="page-header">
          <div>
            <h1>Administration Dashboard (প্রশাসনিক ড্যাশবোর্ড)</h1>
            <p className="page-description">
              Manage admission classes and monitor seat availability.
              (ভর্তি শ্রেণিগুলো পরিচালনা করুন এবং আসনের প্রাপ্যতা
              পর্যবেক্ষণ করুন।)
            </p>
          </div>

          <button
            type="button"
            className="secondary"
            onClick={refreshClasses}
          >
            Refresh Data (ডেটা রিফ্রেশ করুন)
          </button>
        </section>

        <div className="banner error">
          Administrative access is not protected by authentication in this
          version. Do not expose this dashboard publicly as a secure admin
          area. (এই সংস্করণে প্রশাসনিক অ্যাক্সেস কোনো প্রমাণীকরণ দ্বারা
          সুরক্ষিত নয়। নিরাপদ অ্যাডমিন এরিয়া হিসেবে এই ড্যাশবোর্ড প্রকাশ্যে
          উন্মুক্ত করবেন না।)
        </div>

        <Notice
          notice={notice}
          onClose={() => setNotice(null)}
        />

        {classes.error && (
          <div className="banner error" role="alert">
            {errorMessage(classes.error)}
          </div>
        )}

        <section>
          <h2>Admission Overview (ভর্তির সারসংক্ষেপ)</h2>

          {classes.loading && !classes.data ? (
            <p className="loading">
              Loading admission statistics... (ভর্তির পরিসংখ্যান লোড হচ্ছে...)
            </p>
          ) : (
            <div className="stat-grid">
              <div className="stat-card">
                <div className="stat-label">Total Classes (মোট শ্রেণি)</div>
                <div className="stat-value">{totalClasses}</div>
              </div>

              <div className="stat-card">
                <div className="stat-label">Total Seats (মোট আসন)</div>
                <div className="stat-value">{totalSeats}</div>
              </div>

              <div className="stat-card">
                <div className="stat-label">Seats on Hold (সংরক্ষিত আসন)</div>
                <div className="stat-value">{heldSeats}</div>
              </div>

              <div className="stat-card success">
                <div className="stat-label">Confirmed Seats (নিশ্চিত আসন)</div>
                <div className="stat-value">{confirmedSeats}</div>
              </div>

              <div className="stat-card success">
                <div className="stat-label">Available Seats (উপলব্ধ আসন)</div>
                <div className="stat-value">{availableSeats}</div>
              </div>
            </div>
          )}
        </section>

        <section className="panel">
          <div className="panel-header">
            <div>
              <h2>Create a Class (শ্রেণি তৈরি করুন)</h2>
              <p className="muted">
                Add a new class and define its total seat capacity. (নতুন একটি
                শ্রেণি যোগ করুন এবং এর মোট আসন সংখ্যা নির্ধারণ করুন।)
              </p>
            </div>
          </div>

          <AdminPanel
            onCreate={onCreate}
            busy={busy}
          />
        </section>

        <section className="panel">
          <div className="panel-header">
            <div>
              <h2>Class Overview (শ্রেণির সারসংক্ষেপ)</h2>
              <p className="muted">
                Current seat allocation for each class. (প্রতিটি শ্রেণির
                বর্তমান আসন বণ্টন।)
              </p>
            </div>
          </div>

          {classes.loading && classItems.length === 0 ? (
            <p className="loading">Loading classes... (শ্রেণিগুলো লোড হচ্ছে...)</p>
          ) : classItems.length === 0 ? (
            <div className="empty-state">
              <h3>No classes created yet (এখনও কোনো শ্রেণি তৈরি হয়নি)</h3>
              <p>Create your first class using the form above. (উপরের ফর্ম ব্যবহার করে আপনার প্রথম শ্রেণিটি তৈরি করুন।)</p>
            </div>
          ) : (
            <div className="grid">
              {classItems.map((item) => (
                <article className="class-card" key={item.id}>
                  <h3>{item.name}</h3>

                  <dl className="seats">
                    <div>
                      <dt>Total (মোট)</dt>
                      <dd>{item.totalSeats}</dd>
                    </div>

                    <div>
                      <dt>Held (সংরক্ষিত)</dt>
                      <dd>{item.heldSeats}</dd>
                    </div>

                    <div>
                      <dt>Confirmed (নিশ্চিত)</dt>
                      <dd>{item.confirmedSeats}</dd>
                    </div>

                    <div className="avail">
                      <dt>Available (উপলব্ধ)</dt>
                      <dd>{item.availableSeats}</dd>
                    </div>
                  </dl>
                </article>
              ))}
            </div>
          )}
        </section>

        <footer className="site-footer">
          School Admission Portal · Administration (স্কুল ভর্তি পোর্টাল ·
          প্রশাসন)
        </footer>
      </main>
    </>
  );
}

/* Application routes */

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<StudentPortal />} />
        <Route path="/admin" element={<AdminDashboard />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
