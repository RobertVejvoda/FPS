import { describe, expect, it } from 'vitest';
import {
  canAccessTenantAdmin,
  defaultRoute,
  isPlatformPlane,
  moduleLandingRoute,
  tenantLandingRoute,
} from './roles';

// PLAT008A/PLAT008F — the platform-plane gate. The web app infers the platform plane purely from
// the roles array returned by /me (the backend has already gated issuer → roles). The operator
// console itself moved to the private platform repo (#805); the tenant app keeps only the plane
// detection so platform identities never land on tenant pages.

describe('isPlatformPlane', () => {
  it.each(['platform_admin', 'platform_operator', 'platform_auditor'])(
    'treats %s as a platform-plane identity',
    role => {
      expect(isPlatformPlane([role])).toBe(true);
    },
  );

  it.each(['admin', 'hr_manager', 'employee', 'auditor', 'report_viewer'])(
    'treats tenant role %s as tenant-plane',
    role => {
      expect(isPlatformPlane([role])).toBe(false);
    },
  );

  it('rejects an empty roles array', () => {
    expect(isPlatformPlane([])).toBe(false);
  });

  it('matches case-insensitively', () => {
    expect(isPlatformPlane(['Platform_Admin'])).toBe(true);
  });

  it('detects the platform plane even when mixed with tenant roles', () => {
    // Defence-in-depth: the backend never co-issues these, but the gate must still trip.
    expect(isPlatformPlane(['admin', 'platform_operator'])).toBe(true);
  });

  it('does not grant a platform identity any tenant capability', () => {
    expect(canAccessTenantAdmin(['platform_admin'])).toBe(false);
  });
});

describe('defaultRoute', () => {
  it('keeps a platform identity off tenant pages (PLAT008F: console moved to the private platform app)', () => {
    // '/' renders the operator-console-moved notice for platform-plane identities.
    expect(defaultRoute(['platform_admin'])).toBe('/');
    expect(defaultRoute(['platform_auditor'])).toBe('/');
  });

  it('still routes tenant identities to their tenant surface', () => {
    expect(defaultRoute(['employee'])).toBe('/bookings');
    expect(defaultRoute(['admin'])).toBe('/tenant-admin');
  });
});

// PLAT007B — the primary-module routing contract.
describe('moduleLandingRoute', () => {
  it('maps Parking to the role-based default (parking is the whole tenant app today)', () => {
    expect(moduleLandingRoute('Parking', ['employee'])).toBe('/bookings');
    expect(moduleLandingRoute('Parking', ['admin'])).toBe('/tenant-admin');
  });
  it('maps Seats to its own surface', () => {
    expect(moduleLandingRoute('Seats', ['employee'])).toBe('/seats');
  });
});

describe('tenantLandingRoute', () => {
  it('ignores the primary module for a single-module tenant (parking behaviour unchanged)', () => {
    // Green Logistics: Parking primary, only Parking enabled → plain role default.
    expect(tenantLandingRoute(['employee'], 'Parking', ['Parking'])).toBe('/bookings');
    // Even a Seats-primary single-module tenant needs no selector, so it stays on the role default.
    expect(tenantLandingRoute(['employee'], 'Seats', ['Seats'])).toBe('/bookings');
  });
  it('honours the primary module once more than one module is enabled', () => {
    expect(tenantLandingRoute(['employee'], 'Seats', ['Seats', 'Parking'])).toBe('/seats');
    expect(tenantLandingRoute(['employee'], 'Parking', ['Parking', 'Seats'])).toBe('/bookings');
  });
});
