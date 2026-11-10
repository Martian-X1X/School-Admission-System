
import { describe, expect, it } from 'vitest';
import {
  effectiveStatus,
  errorMessage,
  formatRemaining,
  isValidStudentId,
} from './utils.js';

describe('Student ID validation', () => {
  it('accepts exactly 17 digits only', () => {
    expect(isValidStudentId('20152691234567890')).toBe(true);
    expect(isValidStudentId('2015269123456789')).toBe(false);
    expect(isValidStudentId('201526912345678901')).toBe(false);
    expect(isValidStudentId('2015269123456789a')).toBe(false);
    expect(isValidStudentId('')).toBe(false);
  });
});

describe('Remaining time formatting', () => {
  it('formats as mm:ss and never returns a negative value', () => {
    expect(formatRemaining(600000)).toBe('10:00');
    expect(formatRemaining(61000)).toBe('01:01');
    expect(formatRemaining(-5000)).toBe('00:00');
  });
});

describe('Effective application status', () => {
  const now = Date.parse('2026-11-01T04:10:00Z');

  it('changes an overdue HELD application to EXPIRED', () => {
    expect(
      effectiveStatus(
        { status: 'HELD', expiresAt: '2026-11-01T04:09:59Z' },
        now
      )
    ).toBe('EXPIRED');

    expect(
      effectiveStatus(
        { status: 'HELD', expiresAt: '2026-11-01T04:10:30Z' },
        now
      )
    ).toBe('HELD');

    expect(
      effectiveStatus(
        { status: 'CONFIRMED', expiresAt: '2020-01-01T00:00:00Z' },
        now
      )
    ).toBe('CONFIRMED');
  });
});

describe('Error messages', () => {
  it('maps known error codes to readable messages without exposing raw errors', () => {
    expect(errorMessage({ code: 'SEATS_FULL' })).toContain('seats');

    expect(
      errorMessage({ code: 'WHATEVER', message: 'stack trace' })
    ).not.toContain('stack trace');
  });
});
