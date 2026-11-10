import { isValidStudentId } from '../utils.js';

export default function StudentBar({ value, onChange }) {
  const valid = isValidStudentId(value);

  return (
    <section className="card student-bar">
      <label htmlFor="studentId">
        Student Birth Registration Number (17 Digits)
      </label>

      <input
        id="studentId"
        inputMode="numeric"
        maxLength={17}
        placeholder="e.g. 20152691234567890"
        value={value}
        onChange={(e) => onChange(e.target.value.replace(/[^0-9]/g, ''))}
        aria-invalid={value !== '' && !valid}
      />

      {value !== '' && !valid && (
        <p className="hint error-text">
          {value.length}/17 digits — The number must contain exactly 17 digits.
        </p>
      )}

      {valid && (
        <p className="hint ok-text">
          The number is valid. It will be used as the student's identification.
        </p>
      )}
    </section>
  );
}