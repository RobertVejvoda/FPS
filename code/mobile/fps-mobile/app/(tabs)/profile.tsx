import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { useSession } from '@/api/useSession';
import { useProfileSnapshot } from '@/api/useProfileSnapshot';
import type { VehicleSnapshot } from '@/api/profile';
import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';
import { PlaceholderCard } from '@/components/PlaceholderCard';
import { colors, radius, spacing } from '@/theme';

// Profile/settings tab: surfaces authenticated identity plus the employee-safe
// Profile snapshot. Editing remains out of scope for MOB007.
export default function ProfileRoute() {
  const router = useRouter();
  const { apiBaseUrl, clearSession } = useAuth();
  const { state } = useSession();
  const { state: profileState, refresh } = useProfileSnapshot();

  // Must be before any early return to satisfy Rules of Hooks.
  // Redirects to login if the profile snapshot fetch finds the token has expired
  // while the session check still passed (token expired mid-render cycle).
  useEffect(() => {
    if (profileState.kind === 'unauthenticated') {
      clearSession().then(() => router.replace('/login'));
    }
  }, [profileState.kind, clearSession, router]);

  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <Screen>
        <StateView kind="loading" title="Loading profile..." />
      </Screen>
    );
  }

  if (state.kind === 'unauthenticated') {
    return (
      <Screen>
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Your session has expired or was rejected. Please sign in again."
          actionLabel="Sign in"
          onAction={() => router.replace('/login')}
        />
      </Screen>
    );
  }

  if (state.kind === 'unreachable' || state.kind === 'error') {
    return (
      <Screen>
        <StateView
          kind={state.kind}
          title={state.kind === 'unreachable' ? 'Backend unreachable' : 'Profile failed to load'}
          message={state.message}
        />
      </Screen>
    );
  }

  const { me } = state;
  return (
    <Screen scroll>
      <Text style={styles.heading}>Profile</Text>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Identity</Text>
        <FactRow label="User" value={me.userId} />
        <FactRow label="Tenant" value={me.tenantId} />
        <FactRow label="Roles" value={me.roles.length ? me.roles.join(', ') : 'Employee'} />
        <FactRow label="API base URL" value={apiBaseUrl} />
      </View>

      <ProfileSnapshotPanel state={profileState} onRetry={refresh} />

      <PlaceholderCard
        title="Notification preferences"
        description="Reminder and channel preferences land in a later mobile slice."
      />
      <LegalNoticeCard />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Sign out"
        accessibilityHint="Clears your session and returns to the login screen"
        onPress={async () => {
          await clearSession();
          router.replace('/login');
        }}
        style={({ pressed }) => [styles.signOut, pressed && styles.signOutPressed]}
        testID="button-sign-out"
      >
        <Text style={styles.signOutLabel}>Sign out</Text>
      </Pressable>
    </Screen>
  );
}

function LegalNoticeCard() {
  return (
    <View style={styles.card} testID="legal-notice-card">
      <Text style={styles.cardTitle}>Legal</Text>
      <Text style={styles.legalText}>FairSpot is AGPL-3.0-or-later open-source software.</Text>
      <Text style={styles.legalText}>Copyright (c) 2026 Robert Vejvoda.</Text>
      <Text style={styles.legalText}>
        The FairSpot name and logo identify Robert Vejvoda's project; modified or hosted forks must not imply endorsement.
      </Text>
      <Pressable
        accessibilityRole="link"
        accessibilityLabel="Open FairSpot source code"
        onPress={() => { void Linking.openURL('https://github.com/RobertVejvoda/FPS'); }}
        style={({ pressed }) => [styles.legalLink, pressed && styles.signOutPressed]}
      >
        <Text style={styles.legalLinkText}>Source and license</Text>
      </Pressable>
    </View>
  );
}

function ProfileSnapshotPanel({
  state,
  onRetry,
}: {
  state: ReturnType<typeof useProfileSnapshot>['state'];
  onRetry: () => void;
}) {
  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <View style={styles.card}>
        <StateView kind="loading" title="Loading profile facts..." />
      </View>
    );
  }

  if (state.kind === 'unauthenticated') {
    return null;
  }

  if (state.kind === 'notFound') {
    return (
      <PlaceholderCard
        title="Profile details unavailable"
        description="No Profile snapshot exists for this employee yet."
        testID="profile-snapshot-not-found"
      />
    );
  }

  if (state.kind === 'unreachable' || state.kind === 'error') {
    return (
      <View style={styles.card}>
        <StateView
          kind={state.kind}
          title={state.kind === 'unreachable' ? 'Profile service unreachable' : 'Profile details failed to load'}
          message={state.message}
          actionLabel="Retry"
          onAction={onRetry}
          testID="profile-snapshot-error"
        />
      </View>
    );
  }

  const { profile } = state;
  return (
    <>
      <View style={styles.card} testID="profile-eligibility-card">
        <Text style={styles.cardTitle}>Parking Eligibility</Text>
        <FactRow label="Profile status" value={profile.profileStatus} />
        <FactRow label="Parking eligible" value={formatBoolean(profile.parkingEligible)} />
        <FactRow label="Company car" value={formatBoolean(profile.hasCompanyCar)} />
        <FactRow label="Accessibility eligible" value={formatBoolean(profile.accessibilityEligible)} />
        <FactRow label="Reserved space eligible" value={formatBoolean(profile.reservedSpaceEligible)} />
        <FactRow label="Snapshot" value={profile.snapshotVersion} />
      </View>

      <View style={styles.card} testID="profile-vehicles-card">
        <Text style={styles.cardTitle}>Vehicles</Text>
        {profile.vehicles.length === 0 ? (
          <Text style={styles.emptyText}>No active vehicles are linked to this profile.</Text>
        ) : (
          profile.vehicles.map((vehicle) => <VehicleRow key={vehicle.vehicleId} vehicle={vehicle} />)
        )}
      </View>
    </>
  );
}

function FactRow({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

function VehicleRow({ vehicle }: { vehicle: VehicleSnapshot }) {
  return (
    <View style={styles.vehicleRow}>
      <View style={styles.vehicleHeader}>
        <Text style={styles.vehicleTitle}>{vehicle.licensePlate || vehicle.vehicleId}</Text>
        <Text style={styles.vehicleType}>{vehicle.vehicleType}</Text>
      </View>
      <Text style={styles.vehicleMeta}>
        {vehicle.isElectric ? 'Electric' : 'Standard'} - {vehicle.isActive ? 'Active' : 'Inactive'}
      </Text>
    </View>
  );
}

function formatBoolean(value: boolean) {
  return value ? 'Yes' : 'No';
}

const styles = StyleSheet.create({
  heading: { fontSize: 20, fontWeight: '700', color: colors.text },
  card: {
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.sm,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: colors.text,
  },
  row: { gap: spacing.xs },
  rowLabel: { fontSize: 12, color: colors.textMuted, textTransform: 'uppercase', letterSpacing: 0 },
  rowValue: { fontSize: 16, color: colors.text, fontWeight: '500' },
  emptyText: {
    fontSize: 14,
    color: colors.textMuted,
  },
  legalText: {
    fontSize: 14,
    lineHeight: 20,
    color: colors.textMuted,
  },
  legalLink: {
    alignSelf: 'flex-start',
    paddingVertical: spacing.xs,
  },
  legalLinkText: {
    color: colors.primary,
    fontWeight: '700',
  },
  vehicleRow: {
    paddingVertical: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
    gap: spacing.xs,
  },
  vehicleHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: spacing.md,
  },
  vehicleTitle: {
    flex: 1,
    fontSize: 16,
    fontWeight: '600',
    color: colors.text,
  },
  vehicleType: {
    fontSize: 14,
    color: colors.textMuted,
  },
  vehicleMeta: {
    fontSize: 13,
    color: colors.textMuted,
  },
  signOut: {
    marginTop: spacing.lg,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.md,
    minHeight: 44,
    justifyContent: 'center',
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.danger,
    alignItems: 'center',
  },
  signOutPressed: { opacity: 0.7 },
  signOutLabel: { color: colors.danger, fontWeight: '600' },
});
