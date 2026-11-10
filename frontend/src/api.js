const fromWindow =
  typeof window !== 'undefined' && window.APP_CONFIG ? window.APP_CONFIG.API_BASE_URL : '';
const configured = fromWindow && !String(fromWindow).includes('${') ? fromWindow : '';

export const API_BASE_URL = (
  configured ||
  import.meta.env.VITE_API_BASE_URL ||
  'http://localhost:8080'
).replace(/\/+$/, '');

// The browser clock may be wrong, so countdowns use the server's clock (from the Date response header).
let serverOffsetMs = 0;
let offsetKnown = false;
export const serverNow = () => Date.now() + serverOffsetMs;

export class ApiError extends Error {
  constructor(status, code, detail) {
    super(detail || code);
    this.status = status;
    this.code = code;
  }
}

async function request(path, { method = 'GET', studentId, body } = {}) {
  const headers = {};
  if (studentId) headers['X-Student-Id'] = studentId;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  let res;
  try {
    res = await fetch(API_BASE_URL + path, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new ApiError(0, 'NETWORK', 'Cannot reach the server');
  }

  const dateHeader = res.headers.get('date');
  if (dateHeader) {
    const t = Date.parse(dateHeader);
    if (!Number.isNaN(t)) {
      const offset = t - Date.now();
      // Date has 1-second resolution: only adopt it first time or when clearly drifting, to avoid jitter.
      if (!offsetKnown || Math.abs(offset - serverOffsetMs) > 2500) {
        serverOffsetMs = offset;
        offsetKnown = true;
      }
    }
  }

  if (res.status === 204) return null;

  let data = null;
  if ((res.headers.get('content-type') || '').includes('json')) {
    try {
      data = await res.json();
    } catch {
      data = null;
    }
  }

  if (!res.ok) {
    throw new ApiError(res.status, data?.code || `HTTP_${res.status}`, data?.detail || data?.title);
  }
  return data;
}

export const listClasses = () => request('/classes');
export const createClass = (name, totalSeats) =>
  request('/classes', { method: 'POST', body: { name, totalSeats } });
export const applyToClass = (classId, studentId) =>
  request(`/classes/${classId}/holds`, { method: 'POST', studentId });
export const listMyHolds = (studentId) => request('/me/holds', { studentId });
export const confirmHold = (holdId, studentId) =>
  request(`/holds/${holdId}/confirm`, { method: 'POST', studentId });
export const cancelHold = (holdId, studentId) =>
  request(`/holds/${holdId}`, { method: 'DELETE', studentId });
