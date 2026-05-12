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
  cardBackground: '#f9fafb',
} as const;

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
