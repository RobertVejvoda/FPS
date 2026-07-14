import { describe, expect, it } from 'vitest';
import {
  formatRoleClaimNames,
  formatRoleMapping,
  normalizeIdpBrokerAlias,
  parseRoleClaimNames,
  parseRoleMapping,
} from './identityConfigForm';

// AUTH012 (#795) — pure form helpers for the tenant-admin identity settings.

describe('parseRoleClaimNames', () => {
  it('splits on commas and trims whitespace', () => {
    expect(parseRoleClaimNames(' groups , roles ')).toEqual(['groups', 'roles']);
  });

  it('drops empty entries', () => {
    expect(parseRoleClaimNames('groups,, ,roles,')).toEqual(['groups', 'roles']);
    expect(parseRoleClaimNames('')).toEqual([]);
  });

  it('round-trips with formatRoleClaimNames', () => {
    const names = ['groups', 'roles'];
    expect(parseRoleClaimNames(formatRoleClaimNames(names))).toEqual(names);
  });
});

describe('parseRoleMapping', () => {
  it('parses one "group = role" pair per line', () => {
    const result = parseRoleMapping('fairspot-admins = admin\nall-employees = employee');
    expect(result).toEqual({ ok: true, value: { 'fairspot-admins': 'admin', 'all-employees': 'employee' } });
  });

  it('ignores blank lines and trims around the equals sign', () => {
    const result = parseRoleMapping('\n  a-group=admin  \n\n');
    expect(result).toEqual({ ok: true, value: { 'a-group': 'admin' } });
  });

  it('rejects lines without an equals sign', () => {
    const result = parseRoleMapping('not-a-mapping');
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain('not-a-mapping');
  });

  it('rejects lines missing a group or a role', () => {
    expect(parseRoleMapping('= admin').ok).toBe(false);
    expect(parseRoleMapping('group =').ok).toBe(false);
  });

  it('rejects duplicate group names instead of silently keeping one', () => {
    const result = parseRoleMapping('grp = admin\ngrp = employee');
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain('grp');
  });

  it('round-trips with formatRoleMapping', () => {
    const mapping = { 'fairspot-admins': 'admin', staff: 'employee' };
    const result = parseRoleMapping(formatRoleMapping(mapping));
    expect(result).toEqual({ ok: true, value: mapping });
  });

  it('returns an empty mapping for empty input', () => {
    expect(parseRoleMapping('')).toEqual({ ok: true, value: {} });
  });
});

describe('normalizeIdpBrokerAlias', () => {
  it('trims the alias', () => {
    expect(normalizeIdpBrokerAlias('  acme-entra  ')).toBe('acme-entra');
  });

  it('treats empty and whitespace-only input as not configured (null)', () => {
    expect(normalizeIdpBrokerAlias('')).toBeNull();
    expect(normalizeIdpBrokerAlias('   ')).toBeNull();
  });
});
