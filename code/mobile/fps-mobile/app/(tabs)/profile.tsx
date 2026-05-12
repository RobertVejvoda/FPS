import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { useSession } from '@/api/useSession';
import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';
import { PlaceholderCard } from '@/components/PlaceholderCard';
import { colors, radius, spacing } from '@/theme';

// Profile/settings tab: surfaces authenticated identity from GET /me and offers a
// way back to the debug-session screen. Vehicle and preference editing live in
// later slices that depend on Profile API work.
export default function ProfileRoute() {
  const router = useRouter();
  const { apiBaseUrl, clearCredentials } = useAuth();
  const { state } = useSession();

  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <Screen>
        <StateView kind="loading" title="Loading profile…" />
      </Screen>
    );
  }

  if (state.kind === 'unauthenticated') {
    return (
      <Screen>
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Paste a development token to load your profile."
          actionLabel="Open token screen"
          onAction={() => router.push('/debug-session')}
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
    <Screen>
      <Text style={styles.heading}>Profile</Text>
      <View style={styles.row}>
        <Text style={styles.rowLabel}>User</Text>
        <Text style={styles.rowValue}>{me.userId}</Text>
      </View>
      <View style={styles.row}>
        <Text style={styles.rowLabel}>Tenant</Text>
        <Text style={styles.rowValue}>{me.tenantId}</Text>
      </View>
      <View style={styles.row}>
        <Text style={styles.rowLabel}>API base URL</Text>
        <Text style={styles.rowValue}>{apiBaseUrl}</Text>
      </View>
      <PlaceholderCard
        title="Vehicles and eligibility"
        description="Vehicle, accessibility, and company-car details appear here once Profile APIs are surfaced."
      />
      <PlaceholderCard
        title="Notification preferences"
        description="Reminder and channel preferences land in a later mobile slice."
      />
      <Pressable
        accessibilityRole="button"
        onPress={async () => {
          await clearCredentials();
          router.replace('/debug-session');
        }}
        style={({ pressed }) => [styles.signOut, pressed && styles.signOutPressed]}
        testID="button-sign-out"
      >
        <Text style={styles.signOutLabel}>Clear developer session</Text>
      </Pressable>
    </Screen>
  );
}

const styles = StyleSheet.create({
  heading: { fontSize: 20, fontWeight: '700', color: colors.text },
  row: { gap: spacing.xs },
  rowLabel: { fontSize: 12, color: colors.textMuted, textTransform: 'uppercase', letterSpacing: 0.5 },
  rowValue: { fontSize: 16, color: colors.text, fontWeight: '500' },
  signOut: {
    marginTop: spacing.lg,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.danger,
    alignItems: 'center',
  },
  signOutPressed: { opacity: 0.7 },
  signOutLabel: { color: colors.danger, fontWeight: '600' },
});
