import { useEffect, useRef } from 'react';
import { effectiveStatus, formatRemaining, STATUS_LABELS } from '../utils.js';

export default function MyApplications({
  holds,
  now,
  studentValid,
  loading,
  busyId,
  onConfirm,
  onCancel,
  onExpired,
}) {
  const notified = useRef(new Set());

  // When a countdown reaches zero, ask the server for the truth once (no page reload needed).
  useEffect(() => {
    for (const h of holds || []) {
      if (
        h.status === 'HELD' &&
        now >= Date.parse(h.expiresAt) &&
        !notified.current.has(h.holdId)
      ) {
        notified.current.add(h.holdId);
        onExpired?.();
      }
    }
  }, [holds, now, onExpired]);

  if (!studentValid)
    return (
      <p className="muted">
        Enter your 17-digit birth registration number above to view your
        applications. (আপনার আবেদনগুলো দেখতে উপরে শিক্ষার্থীর ১৭ সংখ্যার
        জন্ম নিবন্ধন নম্বরটি লিখুন।)
      </p>
    );

  if (loading && !holds) return <p className="muted">Loading (লোড হচ্ছে)...</p>;

  if (!holds || holds.length === 0)
    return (
      <p className="muted">
        This student has no applications yet. (এই শিক্ষার্থীর এখনও কোনো
        আবেদন নেই।)
      </p>
    );

  return (
    <div className="list">
      {holds.map((h) => {
        const status = effectiveStatus(h, now);
        const isHeld = status === 'HELD';
        const remaining = Date.parse(h.expiresAt) - now;

        return (
          <article
            className={`card hold hold-${status.toLowerCase()}`}
            key={h.holdId}
          >
            <div className="hold-head">
              <h3>{h.className}</h3>
              <span className={`badge badge-${status.toLowerCase()}`}>
                {STATUS_LABELS[status]}
              </span>
            </div>

            {isHeld && (
              <p className="countdown">
                Time remaining (অবশিষ্ট সময়):{' '}
                <strong>{formatRemaining(remaining)}</strong>
              </p>
            )}

            <div className="actions">
              <button
                disabled={!isHeld || busyId === h.holdId}
                onClick={() => onConfirm(h.holdId)}
              >
                Pay Admission Fee (ভর্তি ফি দিন)
              </button>

              <button
                className="secondary"
                disabled={!isHeld || busyId === h.holdId}
                onClick={() => onCancel(h.holdId)}
              >
                Cancel (বাতিল)
              </button>
            </div>
          </article>
        );
      })}
    </div>
  );
}