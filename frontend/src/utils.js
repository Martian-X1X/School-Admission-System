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
  HELD: "Seat on Hold (আসন সংরক্ষিত)",
  CONFIRMED: "Admission Confirmed (ভর্তি নিশ্চিত)",
  EXPIRED: "Expired (মেয়াদোত্তীর্ণ)",
  CANCELLED: "Cancelled (বাতিলকৃত)",
};

const MESSAGES = {
  NETWORK:
    'Unable to connect to the server. Please check whether the server is running and try again. (সার্ভারের সাথে সংযোগ করা যাচ্ছে না। সার্ভার চালু আছে কিনা যাচাই করে আবার চেষ্টা করুন।)',
  SEATS_FULL:
    'Sorry, all seats in this class are currently full. (দুঃখিত, এই শ্রেণিতে এই মুহূর্তে সব আসন পূর্ণ।)',
  ALREADY_APPLIED:
    'This student has already applied for this class (pending or confirmed). (এই শিক্ষার্থী ইতিমধ্যেই এই শ্রেণিতে আবেদন করেছেন — অপেক্ষমাণ বা নিশ্চিত।)',
  HOLD_EXPIRED:
    'This application has expired, and the seat is now available to other students. You may apply again. (এই আবেদনের মেয়াদ শেষ হয়ে গেছে এবং আসনটি এখন অন্যদের জন্য উন্মুক্ত। আপনি আবার আবেদন করতে পারেন।)',
  HOLD_CANCELLED:
    'This application has already been cancelled. (এই আবেদনটি ইতিমধ্যেই বাতিল করা হয়েছে।)',
  ALREADY_CONFIRMED:
    'Admission has already been confirmed, so this application cannot be cancelled. (ভর্তি ইতিমধ্যেই নিশ্চিত হয়েছে, তাই এই আবেদন বাতিল করা যাবে না।)',
  NOT_OWNER:
    'This application does not belong to this student. (এই আবেদনটি এই শিক্ষার্থীর নয়।)',
  INVALID_STUDENT_ID:
    'The birth registration number must contain exactly 17 digits. (জন্ম নিবন্ধন নম্বরটি অবশ্যই ঠিক ১৭ সংখ্যার হতে হবে।)',
  NOT_FOUND:
    'The requested item could not be found. (অনুরোধ করা বিষয়টি খুঁজে পাওয়া যায়নি।)',
  DB_UNAVAILABLE:
    'The server database is currently unavailable. Please try again later. (সার্ভারের ডেটাবেস এই মুহূর্তে অনুপলব্ধ। কিছুক্ষণ পর আবার চেষ্টা করুন।)',
};

export function errorMessage(err) {
  if (err?.code === 'VALIDATION')
    return `Invalid information provided (ভুল তথ্য দেওয়া হয়েছে): ${err.message}`;
  return (
    MESSAGES[err?.code] ||
    'Something went wrong. Please try again later. (কিছু একটা ভুল হয়েছে। কিছুক্ষণ পর আবার চেষ্টা করুন।)'
  );
}