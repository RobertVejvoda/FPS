import { useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuth } from '@/auth/AuthContext';
import { useBookings } from '@/api/useBookings';
import type { BookingListItem } from '@/api/bookings';
import { BookingCard } from '@/components/BookingCard';
import { StateView } from '@/components/StateView';
import { colors, radius, spacing } from '@/theme';

function bookingParams(item: BookingListItem) {
  return {
    pathname: '/booking/[requestId]' as const,
    params: {
      requestId: item.requestId,
      requestedDate: item.requestedDate,
      timeSlotStart: item.timeSlotStart,
      timeSlotEnd: item.timeSlotEnd,
      locationId: item.locationId ?? '',
      status: item.status,
      reason: item.reason ?? '',
      reasonCode: item.reasonCode ?? '',
      allocatedSlotId: item.allocatedSlotId ?? '',
      createdAt: item.createdAt,
      lastStatusChangedAt: item.lastStatusChangedAt,
    },
  };
}

export default function HomeRoute() {
  const router = useRouter();
  const { clearSession } = useAuth();
  const { state, refresh } = useBookings('upcoming');

  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <SafeAreaView style={styles.safe}>
        <StateView kind="loading" title="Loading your spots…" />
      </SafeAreaView>
    );
  }

  if (state.kind === 'unauthenticated') {
    return (
      <SafeAreaView style={styles.safe}>
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Your session has expired. Please sign in again."
          actionLabel="Sign in"
          onAction={async () => {
            await clearSession();
            router.replace('/login');
          }}
        />
      </SafeAreaView>
    );
  }

  if (state.kind === 'unreachable') {
    return (
      <SafeAreaView style={styles.safe}>
        <StateView
          kind="unreachable"
          title="Cannot load your spots"
          message="Please check your connection and try again."
          actionLabel="Retry"
          onAction={refresh}
        />
      </SafeAreaView>
    );
  }

  if (state.kind === 'error') {
    return (
      <SafeAreaView style={styles.safe}>
        <StateView
          kind="error"
          title="Cannot load your spots"
          message="Please check your connection and try again."
          actionLabel="Retry"
          onAction={refresh}
        />
      </SafeAreaView>
    );
  }

  const [primary, ...rest] = state.items;

  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <Text style={styles.heading}>My Spots</Text>

        <Pressable
          style={({ pressed }) => [styles.ctaButton, pressed && styles.ctaButtonPressed]}
          onPress={() => router.push('/(tabs)/new')}
          accessibilityRole="button"
        >
          <Text style={styles.ctaLabel}>+ Request a spot</Text>
        </Pressable>

        {primary ? (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>Upcoming</Text>
            <BookingCard
              booking={primary}
              onPress={() => router.push(bookingParams(primary))}
            />
          </View>
        ) : (
          <View style={styles.emptyCard}>
            <Text style={styles.emptyTitle}>No upcoming spot requests</Text>
            <Text style={styles.emptyHint}>
              Use "Request a spot" above to request a spot for tomorrow or later.
            </Text>
          </View>
        )}

        {rest.length > 0 ? (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>Also upcoming</Text>
            {rest.slice(0, 2).map((item) => (
              <BookingCard
                key={item.requestId}
                booking={item}
                onPress={() => router.push(bookingParams(item))}
              />
            ))}
          </View>
        ) : null}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  scroll: { padding: spacing.lg, gap: spacing.md, flexGrow: 1 },
  heading: { fontSize: 22, fontWeight: '700', color: colors.text },
  ctaButton: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
    minHeight: 48,
    justifyContent: 'center',
  },
  ctaButtonPressed: { opacity: 0.7 },
  ctaLabel: { color: colors.primaryText, fontWeight: '700', fontSize: 16 },
  section: { gap: spacing.sm },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.textMuted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  emptyCard: {
    padding: spacing.lg,
    borderRadius: radius.md,
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.xs,
    alignItems: 'center',
  },
  emptyTitle: { fontSize: 15, fontWeight: '600', color: colors.text, textAlign: 'center' },
  emptyHint: { fontSize: 13, color: colors.textMuted, textAlign: 'center', lineHeight: 18 },
});
