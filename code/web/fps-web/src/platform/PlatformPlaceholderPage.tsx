import { NotWiredBadge } from './NotWiredBadge';

// Reusable honest placeholder for nav targets whose data sources are not implemented in this
// shell slice (Tenants → PLAT008B, Onboarding → PLAT008C, Health → PLAT008D, Audit deep view
// later). It states plainly what is missing and which slice will provide it — no fake grid,
// no fake status.
export function PlatformPlaceholderPage({
  title,
  description,
  slice,
  children,
}: {
  title: string;
  description: string;
  slice?: string;
  children?: React.ReactNode;
}) {
  return (
    <div className="page-stack">
      <section className="page-hero">
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
      </section>
      <section className="plat-card">
        <div className="plat-card-head">
          <h3>{title}</h3>
          <NotWiredBadge slice={slice} />
        </div>
        <p className="plat-muted">
          This surface is part of the platform console build order but its data source is not
          implemented yet. It is shown here as a planned destination, not a broken page.
        </p>
        {children}
      </section>
    </div>
  );
}
