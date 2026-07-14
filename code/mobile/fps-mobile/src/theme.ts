// Minimal shell theme so screens look consistent without pulling in a UI library.
// Refine when product design lands; MOB001 only needs placeholders.
export const colors = {
  background: '#ffffff',
  text: '#111827',
  textMuted: '#6b7280',
  border: '#e5e7eb',
  primary: '#1d4ed8',
  primaryText: '#ffffff',
  danger: '#b91c1c',
  warning: '#92400e',
  // UXPOL001 (#798): success/accent token families so screens stop hardcoding
  // greens. Accent (teal) is the Seats module identity — deliberately distinct
  // from the success green so a seat badge never reads as an outcome state.
  success: '#15803d',
  successText: '#166534',
  successSoft: '#ecfdf5',
  successBorder: '#bbf7d0',
  warningStrong: '#b45309',
  accent: '#0f766e',
  accentSoft: '#f0fdfa',
  accentBorder: '#99f6e4',
  cardBackground: '#f9fafb',
} as const;

// UXPOL001 (#798): minimum touch target for small/link-style actions (iOS HIG
// 44pt; Android accessibility 48dp — 44 is the shared floor we standardize on).
export const touchTarget = 44;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
} as const;

export const radius = {
  sm: 4,
  md: 8,
} as const;
