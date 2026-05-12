import { useRouter } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';
import { useSession } from '@/api/useSession';
import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';
import { PlaceholderCard } from '@/components/PlaceholderCard';
import { colors, spacing } from '@/theme';

// Home / current status. Calls GET /me to verify the dev session and exposes the
// five required shell states (loading, error, unauthenticated, unreachable, ok-with-empty).
export default function HomeRoute() {
  const router = useRouter();
  const { state, refresh } = useSession();

  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <Screen>
        <StateView kind="loading" title="Checking session…" />
      </Screen>
    );
  }

  if (state.kind === 'unauthenticated') {
    return (
      <Screen>
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Your developer token is missing or rejected. Paste a fresh one to continue."
          actionLabel="Open token screen"
          onAction={() => router.push('/debug-session')}
        />
      </Screen>
    );
  }

  if (state.kind === 'unreachable') {
    return (
      <Screen>
        <StateView
          kind="unreachable"
          title="Backend unreachable"
          message={state.message}
          actionLabel="Retry"
          onAction={refresh}
        />
      </Screen>
    );
  }

  if (state.kind === 'error') {
    return (
      <Screen>
        <StateView
          kind="error"
          title="Something went wrong"
          message={state.message}
          actionLabel="Retry"
          onAction={refresh}
        />
      </Screen>
    );
  }

  const { me } = state;
  return (
    <Screen>
      <Text style={styles.heading}>Current Status</Text>
      <View style={styles.identityCard}>
        <Text style={styles.identityLabel}>Signed in as</Text>
        <Text style={styles.identityValue}>{me.userId}</Text>
        <Text style={styles.identityLabel}>Tenant</Text>
        <Text style={styles.identityValue}>{me.tenantId}</Text>
        <Text style={styles.identityLabel}>Roles</Text>
        <Text style={styles.identityValue}>
          {me.roles && me.roles.length > 0 ? me.roles.join(', ') : '—'}
        </Text>
      </View>
      <PlaceholderCard
        title="Active allocation"
        description="The current allocation widget lands in a later booking-flow slice."
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  heading: { fontSize: 20, fontWeight: '700', color: colors.text },
  identityCard: {
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    gap: spacing.xs,
    backgroundColor: colors.cardBackground,
  },
  identityLabel: { fontSize: 12, color: colors.textMuted, textTransform: 'uppercase', letterSpacing: 0.5 },
  identityValue: { fontSize: 16, color: colors.text, fontWeight: '500' },
});
