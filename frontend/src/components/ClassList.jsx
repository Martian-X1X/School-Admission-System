import { effectiveStatus } from '../utils.js';

export default function ClassList({ classes, myHolds, studentValid, now, busyId, onApply, loading }) {
  if (loading && !classes) return <p className="muted">Loading (লোড হচ্ছে)...</p>;

  if (!classes || classes.length === 0)
    return (
      <p className="muted">
        No classes have been created yet. Create a class from the "Admin" tab.
        (এখনও কোনো শ্রেণি তৈরি হয়নি। "Administration" ট্যাব থেকে একটি শ্রেণি
        তৈরি করুন।)
      </p>
    );

  return (
    <div className="grid">
      {classes.map((c) => {
        const active = (myHolds || []).some(
          (h) =>
            h.classId === c.id &&
            ['HELD', 'CONFIRMED'].includes(effectiveStatus(h, now))
        );

        const full = c.availableSeats <= 0;

        let label = 'Apply Now (আবেদন করুন)';
        if (active) label = 'Application Submitted (আবেদন জমা হয়েছে)';
        else if (full) label = 'No Seats Available (কোনো আসন নেই)';

        const disabled = !studentValid || active || full || busyId === c.id;

        return (
          <article className="card class-card" key={c.id}>
            <h3>{c.name}</h3>

            <dl className="seats">
              <div>
                <dt>Total (মোট)</dt>
                <dd>{c.totalSeats}</dd>
              </div>

              <div>
                <dt>On Hold (সংরক্ষিত)</dt>
                <dd>{c.heldSeats}</dd>
              </div>

              <div>
                <dt>Confirmed (নিশ্চিত)</dt>
                <dd>{c.confirmedSeats}</dd>
              </div>

              <div className="avail">
                <dt>Available (উপলব্ধ)</dt>
                <dd>{c.availableSeats}</dd>
              </div>
            </dl>

            <button
              disabled={disabled}
              onClick={() => onApply(c.id)}
            >
              {label}
            </button>

            {!studentValid && (
              <p className="hint">
                Please enter your birth registration number above before
                applying. (আবেদন করার আগে উপরে আপনার জন্ম নিবন্ধন নম্বরটি
                লিখুন।)
              </p>
            )}
          </article>
        );
      })}
    </div>
  );
}