import { useEffect, useState, useCallback } from 'react';
import { api } from './api.js';
import ClassesPanel from './ClassesPanel.jsx';
import MyApplicationsPanel from './MyApplicationsPanel.jsx';
import CreateClassPanel from './CreateClassPanel.jsx';

export default function App() {
  const [studentId, setStudentId] = useState(() => localStorage.getItem('studentId') || '');
  const [tab, setTab] = useState('classes');
  const [banner, setBanner] = useState(null);

  useEffect(() => {
    localStorage.setItem('studentId', studentId);
  }, [studentId]);

  const showError = useCallback((err) => {
    setBanner({ type: 'error', text: err?.message || 'Something went wrong.' });
    setTimeout(() => setBanner(null), 5000);
  }, []);

  const showInfo = useCallback((text) => {
    setBanner({ type: 'info', text });
    setTimeout(() => setBanner(null), 3000);
  }, []);

  return (
    <div className="app">
      <header className="topbar">
        <h1>School Admission System</h1>
        <div className="student-id-field">
          <label htmlFor="studentId">Student Registration No.</label>
          <input
            id="studentId"
            placeholder="17-digit registration number"
            value={studentId}
            onChange={(e) => setStudentId(e.target.value.trim())}
            maxLength={17}
          />
        </div>
      </header>

      {banner && <div className={`banner banner-${banner.type}`}>{banner.text}</div>}

      <nav className="tabs">
        <button className={tab === 'classes' ? 'active' : ''} onClick={() => setTab('classes')}>
          Classes
        </button>
        <button className={tab === 'mine' ? 'active' : ''} onClick={() => setTab('mine')}>
          My Applications
        </button>
        <button className={tab === 'admin' ? 'active' : ''} onClick={() => setTab('admin')}>
          Admin: Create Class
        </button>
      </nav>

      <main>
        {tab === 'classes' && (
          <ClassesPanel studentId={studentId} onError={showError} onInfo={showInfo} />
        )}
        {tab === 'mine' && (
          <MyApplicationsPanel studentId={studentId} onError={showError} onInfo={showInfo} />
        )}
        {tab === 'admin' && <CreateClassPanel onError={showError} onInfo={showInfo} />}
      </main>
    </div>
  );
}
