export function ForbiddenPage() {
  return (
    <div style={{ padding: '2rem', textAlign: 'center', color: 'var(--color-text, #111827)' }}>
      <p style={{ fontWeight: 600, fontSize: '1rem', marginBottom: '0.5rem' }}>
        You do not have access to this page.
      </p>
      <p style={{ color: 'var(--color-text-muted, #6b7280)', fontSize: '0.875rem' }}>
        Contact your tenant administrator if you need access.
      </p>
    </div>
  );
}
