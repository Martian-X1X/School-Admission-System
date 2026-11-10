import { useState } from 'react';

export default function AdminPanel({ onCreate, busy }) {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [localError, setLocalError] = useState('');

  async function submit(e) {
    e.preventDefault();

    const n = Number(seats);

    if (!name.trim()) {
      return setLocalError('Please enter the class name.');
    }

    if (!Number.isInteger(n) || n < 1) {
      return setLocalError(
        'The number of seats must be a positive integer (1 or greater).'
      );
    }

    setLocalError('');

    const ok = await onCreate(name.trim(), n);

    if (ok) {
      setName('');
      setSeats('');
    }
  }

  return (
    <section className="card">
      <h2>Create a New Class (School Administrator)</h2>

      <form onSubmit={submit} className="admin-form">
        <label>
          Class and Shift Name
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Class 6 - Morning Shift"
            maxLength={200}
          />
        </label>

        <label>
          Total Seats
          <input
            type="number"
            min="1"
            value={seats}
            onChange={(e) => setSeats(e.target.value)}
            placeholder="120"
          />
        </label>

        {localError && (
          <p className="error-text">{localError}</p>
        )}

        <button type="submit" disabled={busy}>
          Create Class
        </button>
      </form>
    </section>
  );
}