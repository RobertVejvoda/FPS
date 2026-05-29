import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuth } from '@/auth/AuthContext';
import { useBookings } from '@/api/useBookings';
import { fetchDrawStatus, type BookingListItem, type DrawStatusResult } from '@/api/bookings';
import { BookingCard } from '@/components/BookingCard';
import { StateView } from '@/components/StateView';
import { displaySlot, displayNextDrawRun, shouldShowNextDraw, STATUS_BADGE_LABEL } from '@/displayLabels';
import { colors, radius, spacing } from '@/theme';

function localDateStr(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function sortMixed(items: BookingListItem[]): BookingListItem[] {
  const today = localDateStr(0);
  const todayItems = items.filter(i => i.requestedDate === today);
  const futureItems = items.filter(i => i.requestedDate > today).sort((a, b) => a.requestedDate.localeCompare(b.requestedDate));
  const pastItems = items.filter(i => i.requestedDate < today).sort((a, b) => b.requestedDate.localeCompare(a.requestedDate));
  return [...todayItems, ...futureItems, ...pastItems];
}

const CHIPS = [
  { label: 'Today', offset: 0 },
  { label: 'Tomorrow', offset: 1 },
  { label: 'D+2', offset: 2 },
  { label: 'D+3', offset: 3 },
];

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
  const { apiBaseUrl, bearerToken, clearSession } = useAuth();
  const { state, refresh } = useBookings('all');
  const [selectedChip, setSelectedChip] = useState(0);
  const [drawStatus, setDrawStatus] = useState<DrawStatusResult | null>(null);
  const [drawLoading, setDrawLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setDrawLoading(true);
    setDrawStatus(null);
    fetchDrawStatus({ apiBaseUrl, bearerToken }, localDateStr(selectedChip)).then((result) => {
      if (cancelled) return;
      setDrawLoading(false);
      setDrawStatus(result);
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, selectedChip]);

  const handleUnauthenticated = useCallback(async () => {
    await clearSession();
    router.replace('/login');
  }, [clearSession, router]);

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
          onAction={handleUnauthenticated}
        />
      </SafeAreaView>
    );
  }

  if (state.kind === 'unreachable' || state.kind === 'error') {
    return (
      <SafeAreaView style={styles.safe}>
        <StateView
          kind={state.kind}
          title="Cannot load your spots"
          message="Please check your connection and try again."
          actionLabel="Retry"
          onAction={refresh}
        />
      </SafeAreaView>
    );
  }

  const today = localDateStr(0);
  const tomorrow = localDateStr(1);
  const todayBooking = state.items.find(i => i.requestedDate === today) ?? null;
  const tomorrowBooking = state.items.find(i => i.requestedDate === tomorrow) ?? null;
  const allItems = sortMixed(state.items);

  const demandText = drawLoading ? 'Loading…'
    : drawStatus?.kind === 'ok' ? `Demand: ${drawStatus.demandLevel}`
    : null;
  const canRequestText = drawStatus?.kind === 'ok'
    ? (drawStatus.canRequest ? 'Can request: Yes' : `Can request: No${drawStatus.cannotRequestReason ? ` — ${drawStatus.cannotRequestReason}` : ''}`)
    : null;

  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <Text style={styles.heading}>Home</Text>

        {/* Today / Tomorrow focus cards */}
        <View style={styles.focusRow}>
          <FocusCard label="Today" booking={todayBooking} onPress={todayBooking ? () => router.push(bookingParams(todayBooking)) : undefined} />
          <FocusCard label="Tomorrow" booking={tomorrowBooking} onPress={tomorrowBooking ? () => router.push(bookingParams(tomorrowBooking)) : undefined} />
        </View>

        {/* Quick request chips */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Request a spot</Text>
          <View style={styles.chipRow}>
            {CHIPS.map((chip) => (
              <Pressable
                key={chip.offset}
                onPress={() => setSelectedChip(chip.offset)}
                style={[styles.chip, selectedChip === chip.offset && styles.chipActive]}
                accessibilityRole="button"
              >
                <Text style={[styles.chipText, selectedChip === chip.offset && styles.chipTextActive]}>
                  {chip.label}
                </Text>
              </Pressable>
            ))}
            <Pressable
              onPress={() => router.push('/(tabs)/new')}
              style={styles.chip}
              accessibilityRole="button"
            >
              <Text style={styles.chipText}>More</Text>
            </Pressable>
          </View>
          {drawLoading && <ActivityIndicator size="small" color={colors.primary} style={{ marginTop: spacing.xs }} />}
          {demandText && !drawLoading && <Text style={styles.demandText}>{demandText}</Text>}
          {canRequestText && !drawLoading && <Text style={styles.canRequestText}>{canRequestText}</Text>}
          <Pressable
            onPress={() => router.push({ pathname: '/(tabs)/new', params: { offset: String(selectedChip) } })}
            style={({ pressed }) => [styles.requestBtn, pressed && { opacity: 0.7 }]}
            accessibilityRole="button"
          >
            <Text style={styles.requestBtnText}>
              Request for {CHIPS[selectedChip]?.label ?? 'selected date'}
            </Text>
          </Pressable>
        </View>

        {/* All requests */}
        {allItems.length > 0 ? (
          <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Text style={styles.sectionLabel}>My requests</Text>
              <Text style={styles.sectionCount}>
                {state.items.length}{state.totalCount > state.items.length ? ` of ${state.totalCount}` : ''}
              </Text>
            </View>
            {allItems.map((item) => (
              <BookingCard
                key={item.requestId}
                booking={item}
                onPress={() => router.push(bookingParams(item))}
              />
            ))}
          </View>
        ) : (
          <View style={styles.emptyCard}>
            <Text style={styles.emptyTitle}>No spot requests yet</Text>
            <Text style={styles.emptyHint}>
              Use "Request a spot" above to request a spot for tomorrow or later.
            </Text>
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

function FocusCard({ label, booking, onPress }: {
  label: string;
  booking: BookingListItem | null;
  onPress?: () => void;
}) {
  const badgeLabel = booking ? (STATUS_BADGE_LABEL[booking.status] ?? booking.status) : null;
  const slot = booking ? displaySlot(booking.allocatedSlotId) : null;
  const nextDraw = booking && shouldShowNextDraw(booking.status) ? displayNextDrawRun(booking.requestedDate) : null;

  return (
    <Pressable
      style={({ pressed }) => [styles.focusCard, pressed && onPress ? { opacity: 0.85 } : undefined]}
      onPress={onPress}
      accessibilityRole={onPress ? 'button' : 'none'}
    >
      <Text style={styles.focusDay}>{label}</Text>
      {booking ? (
        <>
          {badgeLabel && <Text style={styles.focusBadge}>{badgeLabel}</Text>}
          {slot && <Text style={styles.focusDetail}>Spot: {slot}</Text>}
          {nextDraw && <Text style={styles.focusNextDraw}>Next draw: {nextDraw}</Text>}
        </>
      ) : (
        <Text style={styles.focusEmpty}>No request yet</Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  scroll: { padding: spacing.lg, gap: spacing.md, flexGrow: 1 },
  heading: { fontSize: 22, fontWeight: '700', color: colors.text },
  focusRow: { flexDirection: 'row', gap: spacing.sm },
  focusCard: {
    flex: 1,
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.xs,
    minHeight: 80,
  },
  focusDay: { fontSize: 11, fontWeight: '700', color: colors.textMuted, textTransform: 'uppercase', letterSpacing: 0.5 },
  focusBadge: { fontSize: 14, fontWeight: '600', color: colors.text },
  focusDetail: { fontSize: 13, color: colors.text, fontWeight: '500' },
  focusNextDraw: { fontSize: 12, color: colors.primary },
  focusEmpty: { fontSize: 13, color: colors.textMuted },
  card: {
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.sm,
  },
  cardTitle: { fontSize: 15, fontWeight: '700', color: colors.text },
  chipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs },
  chip: {
    borderRadius: 20,
    borderWidth: 1,
    borderColor: colors.border,
    paddingVertical: spacing.xs,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.background,
  },
  chipActive: { backgroundColor: colors.primary, borderColor: colors.primary },
  chipText: { fontSize: 13, fontWeight: '500', color: colors.textMuted },
  chipTextActive: { color: colors.primaryText },
  demandText: { fontSize: 13, color: colors.textMuted },
  canRequestText: { fontSize: 13, color: colors.textMuted },
  requestBtn: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    minHeight: 40,
    justifyContent: 'center',
  },
  requestBtnText: { color: colors.primaryText, fontWeight: '700', fontSize: 14 },
  section: { gap: spacing.sm },
  sectionHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.textMuted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  sectionCount: { fontSize: 12, color: colors.textMuted },
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
