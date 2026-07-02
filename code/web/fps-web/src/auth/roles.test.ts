import { describe, expect, it } from 'vitest';
import {
  canAccessPlatformConsole,
  canAccessTenantAdmin,
  canTriagePlatformOnboarding,
  defaultRoute,
  isPlatformAdmin,
  isPlatformPlane,
  moduleLandingRoute,
  tenantLandingRoute,
} from './roles';

// PLAT008A — the platform auth gate. The web app infers the platform plane purely from the
// roles array returned by /me (the backend has already gated issuer → roles). These tests pin
// the gate decisions that drive shell selection, the access-denied state, and role-aware UI.

describe('isPlatformPlane / canAccessPlatformConsole', () => {
  it.each(['platform_admin', 'platform_operator', 'platform_auditor'])(
    'treats %s as a platform-plane identity',
    role => {
      expect(isPlatformPlane([role])).toBe(true);
      expect(canAccessPlatformConsole([role])).toBe(true);
    },
  );

  it.each(['admin', 'hr_manager', 'employee', 'auditor', 'report_viewer'])(
    'rejects tenant role %s from the platform console',
    role => {
      expect(isPlatformPlane([role])).toBe(false);
      expect(canAccessPlatformConsole([role])).toBe(false);
    },
  );

  it('rejects an empty roles array', () => {
    expect(isPlatformPlane([])).toBe(false);
    expect(canAccessPlatformConsole([])).toBe(false);
  });

  it('matches case-insensitively', () => {
    expect(isPlatformPlane(['Platform_Admin'])).toBe(true);
  });

  it('detects the platform plane even when mixed with tenant roles', () => {
    // Defence-in-depth: the backend never co-issues these, but the gate must still trip.
    expect(isPlatformPlane(['admin', 'platform_operator'])).toBe(true);
  });
});

describe('platform role-aware helpers', () => {
  it('shows $ cost only to platform_admin', () => {
    expect(isPlatformAdmin(['platform_admin'])).toBe(true);
    expect(isPlatformAdmin(['platform_operator'])).toBe(false);
    expect(isPlatformAdmin(['platform_auditor'])).toBe(false);
  });

  it('allows onboarding triage for admin and operator, not auditor', () => {
    expect(canTriagePlatformOnboarding(['platform_admin'])).toBe(true);
    expect(canTriagePlatformOnboarding(['platform_operator'])).toBe(true);
    expect(canTriagePlatformOnboarding(['platform_auditor'])).toBe(false);
  });

  it('does not grant a tenant admin any platform capability', () => {
    expect(isPlatformAdmin(['admin'])).toBe(false);
    expect(canTriagePlatformOnboarding(['admin'])).toBe(false);
    // and a platform identity is not a tenant admin
    expect(canAccessTenantAdmin(['platform_admin'])).toBe(false);
  });
});

describe('defaultRoute', () => {
  it('lands a platform identity in the operator console', () => {
    expect(defaultRoute(['platform_admin'])).toBe('/platform/overview');
    expect(defaultRoute(['platform_auditor'])).toBe('/platform/overview');
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
