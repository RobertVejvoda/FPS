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
import { formatRoles } from '@/displayLabels';
import { colors, radius, spacing } from '@/theme';

export default function MoreRoute() {
  const router = useRouter();
  const { clearSession } = useAuth();
  const { state: sessionState } = useSession();
  const { state: profileState, refresh } = useProfileSnapshot();

  useEffect(() => {
    if (profileState.kind === 'unauthenticated') {
      clearSession().then(() => router.replace('/login'));
    }
  }, [profileState.kind, clearSession, router]);

  if (sessionState.kind === 'idle' || sessionState.kind === 'loading') {
    return (
      <Screen>
        <StateView kind="loading" title="Loading…" />
      </Screen>
    );
  }

  if (sessionState.kind === 'unauthenticated') {
    return (
      <Screen>
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Your session has expired. Please sign in again."
          actionLabel="Sign in"
          onAction={() => router.replace('/login')}
        />
      </Screen>
    );
  }

  if (sessionState.kind === 'unreachable' || sessionState.kind === 'error') {
    return (
      <Screen>
        <StateView
          kind={sessionState.kind}
          title="Cannot load your account"
          message="Please check your connection and try again."
        />
      </Screen>
    );
  }

  const { me } = sessionState;

  return (
    <Screen scroll>
      <Text style={styles.heading}>More</Text>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Your Account</Text>
        <FactRow label="Role" value={formatRoles(me.roles ?? [])} />
      </View>

      <EligibilityPanel state={profileState} onRetry={refresh} />

      <PlaceholderCard
        title="Notification preferences"
        description="Reminder and channel preferences land in a later mobile slice."
      />

      <AboutCard />

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Sign out"
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

function AboutCard() {
  return (
    <View style={styles.card}>
      <Text style={styles.cardTitle}>About FairSpot</Text>
      <Text style={styles.bodyText}>FairSpot is AGPL-3.0-or-later open-source software.</Text>
      <Text style={styles.bodyText}>Copyright © 2026 Robert Vejvoda.</Text>
      <Text style={styles.bodyText}>
        The FairSpot name and logo identify Robert Vejvoda's project; modified or hosted forks must not imply endorsement.
      </Text>
      <Pressable
        accessibilityRole="link"
        accessibilityLabel="Open FairSpot source code"
        onPress={() => { void Linking.openURL('https://github.com/RobertVejvoda/FPS'); }}
        style={({ pressed }) => [styles.link, pressed && { opacity: 0.6 }]}
      >
        <Text style={styles.linkText}>Source code and license</Text>
      </Pressable>
    </View>
  );
}

function EligibilityPanel({
  state,
  onRetry,
}: {
  state: ReturnType<typeof useProfileSnapshot>['state'];
  onRetry: () => void;
}) {
  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <View style={styles.card}>
        <StateView kind="loading" title="Loading profile…" />
      </View>
    );
  }

  if (state.kind === 'unauthenticated') return null;

  if (state.kind === 'notFound') {
    return (
      <PlaceholderCard
        title="Spot eligibility unavailable"
        description="No spot profile exists for this account yet."
        testID="profile-snapshot-not-found"
      />
    );
  }

  if (state.kind === 'unreachable' || state.kind === 'error') {
    return (
      <View style={styles.card}>
        <StateView
          kind={state.kind}
          title="Spot profile unavailable"
          message="Please check your connection and try again."
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
        <Text style={styles.cardTitle}>Spot Eligibility</Text>
        <FactRow label="Spot eligible" value={formatBoolean(profile.parkingEligible)} />
        <FactRow label="Accessible spot eligible" value={formatBoolean(profile.accessibilityEligible)} />
        <FactRow label="Reserved space eligible" value={formatBoolean(profile.reservedSpaceEligible)} />
        <FactRow label="Company car on file" value={formatBoolean(profile.hasCompanyCar)} />
      </View>

      <View style={styles.card} testID="profile-vehicles-card">
        <Text style={styles.cardTitle}>Your Vehicles</Text>
        {profile.vehicles.length === 0 ? (
          <Text style={styles.bodyText}>No vehicles are linked to your profile.</Text>
        ) : (
          profile.vehicles.map((v) => <VehicleRow key={v.vehicleId} vehicle={v} />)
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
        <Text style={styles.vehiclePlate}>{vehicle.licensePlate || 'Unknown plate'}</Text>
        <Text style={styles.vehicleType}>{vehicle.vehicleType}</Text>
      </View>
      <Text style={styles.vehicleMeta}>
        {vehicle.isElectric ? 'Electric' : 'Standard'} · {vehicle.isActive ? 'Active' : 'Inactive'}
      </Text>
    </View>
  );
}

function formatBoolean(value: boolean) {
  return value ? 'Yes' : 'No';
}

const styles = StyleSheet.create({
  heading: { fontSize: 22, fontWeight: '700', color: colors.text },
  card: {
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.sm,
  },
  cardTitle: { fontSize: 16, fontWeight: '700', color: colors.text },
  row: { gap: 2 },
  rowLabel: { fontSize: 12, color: colors.textMuted, textTransform: 'uppercase', letterSpacing: 0 },
  rowValue: { fontSize: 15, color: colors.text, fontWeight: '500' },
  bodyText: { fontSize: 14, lineHeight: 20, color: colors.textMuted },
  link: { alignSelf: 'flex-start', paddingVertical: spacing.xs },
  linkText: { color: colors.primary, fontWeight: '700', fontSize: 14 },
  vehicleRow: {
    paddingVertical: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
    gap: spacing.xs,
  },
  vehicleHeader: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md },
  vehiclePlate: { flex: 1, fontSize: 15, fontWeight: '600', color: colors.text },
  vehicleType: { fontSize: 13, color: colors.textMuted },
  vehicleMeta: { fontSize: 13, color: colors.textMuted },
  signOut: {
    marginTop: spacing.lg,
    paddingVertical: spacing.md,
    minHeight: 44,
    justifyContent: 'center',
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.danger,
    alignItems: 'center',
  },
  signOutPressed: { opacity: 0.7 },
  signOutLabel: { color: colors.danger, fontWeight: '600', fontSize: 15 },
});
