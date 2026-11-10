import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import MyApplications from './MyApplications.jsx';

const NOW = Date.parse('2026-11-01T04:10:00Z');
const base = {
  holdId: 'h1',
  classId: 'c1',
  className: 'Class 6 - Morning Shift',
  status: 'HELD',
};

describe('MyApplications', () => {
  it('shows a countdown and enables the pay button while the hold is active', () => {
    render(
      <MyApplications
        studentValid
        now={NOW}
        holds={[{ ...base, expiresAt: '2026-11-01T04:15:00Z' }]}
      />
    );

    expect(screen.getByText('05:00')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Pay Admission Fee (ভর্তি ফি দিন)' })
    ).toBeEnabled();
  });

  it('shows EXPIRED and disables the pay button when time is up, without a reload', () => {
    render(
      <MyApplications
        studentValid
        now={NOW}
        holds={[{ ...base, expiresAt: '2026-11-01T04:09:00Z' }]}
      />
    );

    expect(screen.getByText('Expired (মেয়াদোত্তীর্ণ)')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Pay Admission Fee (ভর্তি ফি দিন)' })
    ).toBeDisabled();
    expect(
      screen.getByRole('button', { name: 'Cancel (বাতিল)' })
    ).toBeDisabled();
  });

  it('asks the user for a student ID when none is valid', () => {
    render(
      <MyApplications studentValid={false} now={NOW} holds={null} />
    );

    expect(
      screen.getByText(/17-digit birth registration number/)
    ).toBeInTheDocument();
  });
});