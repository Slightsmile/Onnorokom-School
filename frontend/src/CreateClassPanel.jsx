import { useState } from 'react';
import { api } from './api.js';

export default function CreateClassPanel({ onError, onInfo }) {
  const [name, setName] = useState('');
  const [totalSeats, setTotalSeats] = useState('');
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    const seats = parseInt(totalSeats, 10);
    if (!name.trim() || !seats || seats <= 0) {
      onError(new Error('Please provide a class name and a positive seat count.'));
      return;
    }
    setSubmitting(true);
    try {
      await api.createClass(name.trim(), seats);
      onInfo(`Class "${name.trim()}" created.`);
      setName('');
      setTotalSeats('');
    } catch (err) {
      onError(err);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="panel">
      <h2>Create a New Class / Shift</h2>
      <form onSubmit={handleSubmit} className="form">
        <label>
          Class name
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Class 6 - Morning Shift"
          />
        </label>
        <label>
          Total seats
          <input
            type="number"
            min="1"
            value={totalSeats}
            onChange={(e) => setTotalSeats(e.target.value)}
            placeholder="e.g. 120"
          />
        </label>
        <button type="submit" disabled={submitting}>
          {submitting ? 'Creating...' : 'Create Class'}
        </button>
      </form>
    </div>
  );
}
