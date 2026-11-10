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
  HELD: 'অপেক্ষমাণ (আসন ধরে রাখা হয়েছে)',
  CONFIRMED: 'ভর্তি নিশ্চিত',
  EXPIRED: 'মেয়াদ শেষ',
  CANCELLED: 'বাতিল',
};

const MESSAGES = {
  NETWORK:
    'সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। সার্ভার চালু আছে কিনা দেখুন, তারপর আবার চেষ্টা করুন।',
  SEATS_FULL: 'দুঃখিত, এই ক্লাসের সব আসন পূর্ণ হয়ে গেছে।',
  ALREADY_APPLIED:
    'এই শিক্ষার্থী এই ক্লাসে ইতিমধ্যে আবেদন করেছে (অপেক্ষমাণ বা নিশ্চিত)।',
  HOLD_EXPIRED:
    'আবেদনের মেয়াদ শেষ হয়ে গেছে এবং আসনটি অন্যদের জন্য খুলে গেছে। চাইলে আবার আবেদন করুন।',
  HOLD_CANCELLED: 'এই আবেদনটি আগেই বাতিল করা হয়েছে।',
  ALREADY_CONFIRMED: 'ভর্তি ইতিমধ্যে নিশ্চিত হয়ে গেছে, তাই বাতিল করা যাবে না।',
  NOT_OWNER: 'এই আবেদনটি এই শিক্ষার্থীর নয়।',
  INVALID_STUDENT_ID: 'জন্ম নিবন্ধন নম্বর অবশ্যই ১৭ অঙ্কের হতে হবে।',
  NOT_FOUND: 'যা খুঁজছেন তা পাওয়া যায়নি।',
  DB_UNAVAILABLE: 'সার্ভারের ডেটাবেস এই মুহূর্তে পাওয়া যাচ্ছে না। একটু পরে আবার চেষ্টা করুন।',
};

export function errorMessage(err) {
  if (err?.code === 'VALIDATION') return `দেওয়া তথ্য সঠিক নয়: ${err.message}`;
  return MESSAGES[err?.code] || 'কিছু একটা সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।';
}
