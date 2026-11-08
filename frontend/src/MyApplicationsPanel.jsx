import { useEffect, useState, useCallback } from 'react';
import { api } from './api.js';

const POLL_INTERVAL_MS = 3000;

function formatCountdown(expiresAt, now) {
  const diffMs = new Date(expiresAt).getTime() - now;
  if (diffMs <= 0) return 'expired';
  const totalSeconds = Math.floor(diffMs / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}m ${seconds.toString().padStart(2, '0')}s`;
}

export default function MyApplicationsPanel({ studentId, onError, onInfo }) {
  const [holds, setHolds] = useState([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);
  const [now, setNow] = useState(Date.now());

  const load = useCallback(async () => {
    if (!studentId) {
      setHolds([]);
      setLoading(false);
      return;
    }
    try {
      const data = await api.listMyHolds(studentId);
      setHolds(data);
    } catch (err) {
      onError(err);
    } finally {
      setLoading(false);
    }
  }, [studentId, onError]);

  useEffect(() => {
    load();
    const interval = setInterval(load, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [load]);

  useEffect(() => {
    const tick = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(tick);
  }, []);

  async function handleConfirm(holdId) {
    setBusyId(holdId);
    try {
      await api.confirmHold(holdId, studentId);
      onInfo('Admission fee paid. Seat confirmed!');
      await load();
    } catch (err) {
      onError(err);
    } finally {
      setBusyId(null);
    }
  }

  async function handleCancel(holdId) {
    setBusyId(holdId);
    try {
      await api.cancelHold(holdId, studentId);
      onInfo('Application cancelled.');
      await load();
    } catch (err) {
      onError(err);
    } finally {
      setBusyId(null);
    }
  }

  if (!studentId) {
    return <div className="panel">Enter your student registration number to view your applications.</div>;
  }

  if (loading) return <div className="panel">Loading your applications...</div>;

  return (
    <div className="panel">
      <h2>My Applications</h2>
      {holds.length === 0 && <p>You have no applications yet.</p>}
      <table className="table">
        <thead>
          <tr>
            <th>Class</th>
            <th>Status</th>
            <th>Time left</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {holds.map((h) => (
            <tr key={h.holdId}>
              <td>{h.className}</td>
              <td>
                <span className={`status status-${h.status.toLowerCase()}`}>{h.status}</span>
              </td>
              <td>{h.status === 'HELD' ? formatCountdown(h.expiresAt, now) : '-'}</td>
              <td>
                {h.status === 'HELD' && (
                  <>
                    <button disabled={busyId === h.holdId} onClick={() => handleConfirm(h.holdId)}>
                      Pay Admission Fee
                    </button>
                    <button disabled={busyId === h.holdId} onClick={() => handleCancel(h.holdId)}>
                      Cancel
                    </button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
