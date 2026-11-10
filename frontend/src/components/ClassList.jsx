import { effectiveStatus } from '../utils.js';

export default function ClassList({ classes, myHolds, studentValid, now, busyId, onApply, loading }) {
  if (loading && !classes) return <p className="muted">Loading...</p>;

  if (!classes || classes.length === 0)
    return (
      <p className="muted">
        No classes have been created yet. Create a class from the "Admin" tab.
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

        let label = 'Apply Now';
        if (active) label = 'Application Submitted';
        else if (full) label = 'No Seats Available';

        const disabled = !studentValid || active || full || busyId === c.id;

        return (
          <article className="card class-card" key={c.id}>
            <h3>{c.name}</h3>

            <dl className="seats">
              <div>
                <dt>Total</dt>
                <dd>{c.totalSeats}</dd>
              </div>

              <div>
                <dt>On Hold</dt>
                <dd>{c.heldSeats}</dd>
              </div>

              <div>
                <dt>Confirmed</dt>
                <dd>{c.confirmedSeats}</dd>
              </div>

              <div className="avail">
                <dt>Available</dt>
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
                Please enter your birth registration number above before applying.
              </p>
            )}
          </article>
        );
      })}
    </div>
  );
}