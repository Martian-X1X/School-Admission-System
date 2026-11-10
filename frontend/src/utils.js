export const isValidStudentId = (value) => /^[0-9]{17}$/.test(value || '');

export function formatRemaining(ms) {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const m = Math.floor(total / 60);
  const s = total % 60;
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

// A HELD application whose time is up is shown as EXPIRED immediately, without a reload.
export function effectiveStatus(hold, nowMs) {
  if (hold.status === 'HELD' && nowMs >= Date.parse(hold.expiresAt)) return 'EXPIRED';
  return hold.status;
}

export const STATUS_LABELS = {
  HELD: "Seat on Hold",
  CONFIRMED: "Admission Confirmed",
  EXPIRED: "Expired",
  CANCELLED: "Cancelled",
};

const MESSAGES = {
  NETWORK:
    'Unable to connect to the server. Please check whether the server is running and try again.',
  SEATS_FULL: 'Sorry, all seats in this class are currently full.',
  ALREADY_APPLIED:
    'This student has already applied for this class (pending or confirmed).',
  HOLD_EXPIRED:
    'This application has expired, and the seat is now available to other students. You may apply again.',
  HOLD_CANCELLED: 'This application has already been cancelled.',
  ALREADY_CONFIRMED: 'Admission has already been confirmed, so this application cannot be cancelled.',
  NOT_OWNER: 'This application does not belong to this student.',
  INVALID_STUDENT_ID: 'The birth registration number must contain exactly 17 digits.',
  NOT_FOUND: 'The requested item could not be found.',
  DB_UNAVAILABLE: 'The server database is currently unavailable. Please try again later.',
};

export function errorMessage(err) {
  if (err?.code === 'VALIDATION') return `Invalid information provided: ${err.message}`;
  return MESSAGES[err?.code] || 'Something went wrong. Please try again later.';
}