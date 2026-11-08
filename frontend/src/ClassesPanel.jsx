import { useEffect, useState, useCallback } from 'react';
import { api } from './api.js';

const POLL_INTERVAL_MS = 3000;

export default function ClassesPanel({ studentId, onError, onInfo }) {
  const [classes, setClasses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [applyingId, setApplyingId] = useState(null);

  const load = useCallback(async () => {
    try {
      const data = await api.listClasses();
      setClasses(data);
    } catch (err) {
      onError(err);
    } finally {
      setLoading(false);
    }
  }, [onError]);

  useEffect(() => {
    load();
    const interval = setInterval(load, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [load]);

  async function handleApply(classId) {
    if (!studentId || studentId.length !== 17) {
      onError(new Error('Please enter your 17-digit student registration number first.'));
      return;
    }
    setApplyingId(classId);
    try {
      await api.createHold(classId, studentId);
      onInfo('Application submitted. Check "My Applications" to pay the fee before it expires.');
      await load();
    } catch (err) {
      onError(err);
    } finally {
      setApplyingId(null);
    }
  }

  if (loading) return <div className="panel">Loading classes...</div>;

  return (
    <div className="panel">
      <h2>Available Classes</h2>
      {classes.length === 0 && <p>No classes have been created yet.</p>}
      <table className="table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Total</th>
            <th>Held</th>
            <th>Confirmed</th>
            <th>Available</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {classes.map((c) => (
            <tr key={c.id}>
              <td>{c.name}</td>
              <td>{c.totalSeats}</td>
              <td>{c.heldSeats}</td>
              <td>{c.confirmedSeats}</td>
              <td>{c.availableSeats}</td>
              <td>
                <button
                  disabled={c.availableSeats <= 0 || applyingId === c.id}
                  onClick={() => handleApply(c.id)}
                >
                  {applyingId === c.id ? 'Applying...' : 'Apply'}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
