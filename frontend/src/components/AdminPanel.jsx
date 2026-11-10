import { useState } from 'react';

export default function AdminPanel({ onCreate, busy }) {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [localError, setLocalError] = useState('');

  async function submit(e) {
    e.preventDefault();

    const n = Number(seats);

    if (!name.trim()) {
      return setLocalError('Please enter the class name. (অনুগ্রহ করে শ্রেণির নাম লিখুন।)');
    }

    if (!Number.isInteger(n) || n < 1) {
      return setLocalError(
        'The number of seats must be a positive integer (1 or greater). (আসন সংখ্যা অবশ্যই ১ বা তার বেশি পূর্ণসংখ্যা হতে হবে।)'
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
      <h2>Create a New Class (নতুন শ্রেণি তৈরি করুন)</h2>

      <form onSubmit={submit} className="admin-form">
        <label>
          Class and Shift Name (শ্রেণি ও শিফটের নাম)
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Class 6 - Morning Shift"
            maxLength={200}
          />
        </label>

        <label>
          Total Seats (মোট আসন)
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
          Create Class (শ্রেণি তৈরি করুন)
        </button>
      </form>
    </section>
  );
}