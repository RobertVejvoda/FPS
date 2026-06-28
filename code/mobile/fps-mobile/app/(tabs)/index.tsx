import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuth } from '@/auth/AuthContext';
import { useBookings } from '@/api/useBookings';
import { cancelBooking, confirmBookingUsage, type BookingListItem } from '@/api/bookings';
import { fetchDrawStatus, type DrawStatusResult } from '@/api/draws';
import { StateView } from '@/components/StateView';
import { displaySlot, STATUS_BADGE_LABEL, formatCutOffAt } from '@/displayLabels';
import { DEMO_LOCATION_ID, DEFAULT_TIME_SLOT_START, DEFAULT_TIME_SLOT_END } from '@/demoDefaults';
import { colors, radius, spacing } from '@/theme';

function dateStr(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function isWorkday(d: Date): boolean {
  const day = d.getDay();
  return day >= 1 && day <= 5;
}

// Four day focus cards: today, tomorrow, then the next two working days
// (weekends skipped), matching the web My Spots model. Labels use Today/Tomorrow
// then the weekday name — never D+2/D+3. Each card carries its real date + the
// calendar offset used by the request form.
function workdayCards(count = 4): Array<{ label: string; date: string; offset: number }> {
  const base = new Date();
  base.setHours(0, 0, 0, 0);
  const out: Array<{ label: string; date: string; offset: number }> = [];
  const cand = new Date(base);
  while (out.length < count) {
    if (isWorkday(cand)) {
      const offset = Math.round((cand.getTime() - base.getTime()) / 86_400_000);
      const label = offset === 0 ? 'Today'
        : offset === 1 ? 'Tomorrow'
        : cand.toLocaleDateString(undefined, { weekday: 'long' });
      out.push({ label, date: dateStr(cand), offset });
    }
    cand.setDate(cand.getDate() + 1);
  }
  return out;
}

const DAYS = workdayCards(4);

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
  const [drawStatuses, setDrawStatuses] = useState<(DrawStatusResult | null)[]>([]);
  const [drawLoading, setDrawLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [toastMsg, setToastMsg] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setDrawLoading(true);
    setDrawStatuses(DAYS.map(() => null));
    Promise.all(
      DAYS.map(day => fetchDrawStatus({ apiBaseUrl, bearerToken }, {
        date: day.date,
        locationId: DEMO_LOCATION_ID,
        timeSlotStart: DEFAULT_TIME_SLOT_START,
        timeSlotEnd: DEFAULT_TIME_SLOT_END,
      }))
    ).then(results => {
      if (cancelled) return;
      setDrawStatuses(results);
      setDrawLoading(false);
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken]);

  function showToast(msg: string) {
    setToastMsg(msg);
    setTimeout(() => setToastMsg(null), 3500);
  }

  const handleCancel = useCallback(async (requestId: string) => {
    Alert.alert('Cancel request', 'Are you sure you want to cancel this spot request?', [
      { text: 'Keep it', style: 'cancel' },
      {
        text: 'Cancel request', style: 'destructive',
        onPress: async () => {
          setBusyId(requestId);
          const result = await cancelBooking({ apiBaseUrl, bearerToken }, requestId);
          setBusyId(null);
          if (result.kind === 'ok') { showToast('Request cancelled.'); refresh(); }
          else if (result.kind === 'unauthenticated') { await clearSession(); router.replace('/login'); }
          else showToast('message' in result ? result.message : 'Could not cancel.');
        },
      },
    ]);
  }, [apiBaseUrl, bearerToken, clearSession, refresh, router]);

  const handleConfirm = useCallback(async (requestId: string) => {
    setBusyId(requestId);
    const result = await confirmBookingUsage({ apiBaseUrl, bearerToken }, requestId);
    setBusyId(null);
    if (result.kind === 'confirmed') {
      showToast(result.wasAlreadyConfirmed ? 'Usage was already recorded.' : 'Usage confirmed.');
      refresh();
    } else if (result.kind === 'unauthenticated') {
      await clearSession();
      router.replace('/login');
    } else {
      showToast('message' in result ? result.message : 'Could not confirm usage.');
    }
  }, [apiBaseUrl, bearerToken, clearSession, refresh, router]);

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

  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <Text style={styles.heading}>My Spots</Text>

        {toastMsg && (
          <View style={styles.toast}>
            <Text style={styles.toastText}>{toastMsg}</Text>
          </View>
        )}

        {/* Three day tiles */}
        {DAYS.map((day, i) => {
          const date = day.date;
          const booking = state.items.find(b => b.requestedDate === date) ?? null;
          const drawStatus = drawStatuses[i] ?? null;
          return (
            <DayTile
              key={day.offset}
              label={day.label}
              date={date}
              booking={booking}
              drawStatus={drawStatus}
              drawLoading={drawLoading}
              busy={busyId === booking?.requestId}
              onCancel={booking?.nextAction === 'cancel' ? () => handleCancel(booking.requestId) : undefined}
              onConfirm={booking?.nextAction === 'confirmUsage' ? () => handleConfirm(booking.requestId) : undefined}
              onRequest={() => router.push({ pathname: '/(tabs)/new', params: { offset: String(day.offset) } })}
              onDetails={booking ? () => router.push(bookingParams(booking)) : undefined}
            />
          );
        })}

        {/* Secondary navigation */}
        <Pressable
          onPress={() => router.push('/(tabs)/bookings')}
          style={({ pressed }) => [styles.historyLink, pressed && { opacity: 0.6 }]}
          accessibilityRole="button"
        >
          <Text style={styles.historyLinkText}>History &amp; all requests →</Text>
        </Pressable>
      </ScrollView>
    </SafeAreaView>
  );
}

function DayTile({ label, date, booking, drawStatus, drawLoading, busy, onCancel, onConfirm, onRequest, onDetails }: {
  label: string;
  date: string;
  booking: BookingListItem | null;
  drawStatus: DrawStatusResult | null;
  drawLoading: boolean;
  busy: boolean;
  onCancel?: () => void;
  onConfirm?: () => void;
  onRequest?: () => void;
  onDetails?: () => void;
}) {
  const scheduleOk = drawStatus?.kind === 'ok' ? drawStatus.data : null;
  const badgeLabel = booking ? (STATUS_BADGE_LABEL[booking.status] ?? booking.status) : null;
  const slot = booking ? displaySlot(booking.allocatedSlotId) : null;
  const d = new Date(date + 'T00:00:00');
  const dateLabel = d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

  return (
    <View style={styles.tile}>
      {/* Header row */}
      <View style={styles.tileHeader}>
        <View>
          <Text style={styles.tileDay}>{label}</Text>
          <Text style={styles.tileDate}>{dateLabel}</Text>
        </View>
        {badgeLabel && <Text style={styles.tileBadge}>{badgeLabel}</Text>}
      </View>

      {/* Allocated slot */}
      {slot && <Text style={styles.tileSlot}>Spot: {slot}</Text>}

      {/* Schedule timing */}
      {drawLoading && <ActivityIndicator size="small" color={colors.primary} style={{ marginTop: spacing.xs }} />}
      {!drawLoading && scheduleOk?.nextDrawAt && (
        <Text style={styles.tileSchedule}>Draw: {formatCutOffAt(scheduleOk.nextDrawAt, scheduleOk.timeZone)}</Text>
      )}
      {!drawLoading && scheduleOk?.cutOffAt && (
        <Text style={styles.tileSchedule}>Cut-off: {formatCutOffAt(scheduleOk.cutOffAt, scheduleOk.timeZone)}</Text>
      )}
      {!drawLoading && scheduleOk && !booking && scheduleOk.safeMessage && (
        <Text style={styles.tileSchedule}>{scheduleOk.safeMessage}</Text>
      )}

      {/* Primary action */}
      {booking ? (
        <View style={styles.tileActions}>
          {onConfirm && (
            <Pressable
              onPress={onConfirm}
              disabled={busy}
              style={({ pressed }) => [styles.confirmBtn, (busy || pressed) && { opacity: 0.6 }]}
              accessibilityRole="button"
            >
              <Text style={styles.confirmBtnText}>{busy ? 'Confirming…' : 'Confirm usage'}</Text>
            </Pressable>
          )}
          {onCancel && (
            <Pressable
              onPress={onCancel}
              disabled={busy}
              style={({ pressed }) => [styles.cancelBtn, (busy || pressed) && { opacity: 0.6 }]}
              accessibilityRole="button"
            >
              <Text style={styles.cancelBtnText}>{busy ? 'Cancelling…' : 'Cancel'}</Text>
            </Pressable>
          )}
          {!onCancel && !onConfirm && onDetails && (
            <Pressable onPress={onDetails} accessibilityRole="button">
              <Text style={styles.detailsLink}>View details →</Text>
            </Pressable>
          )}
        </View>
      ) : !drawLoading && scheduleOk?.canRequest ? (
        <Pressable
          onPress={onRequest}
          style={({ pressed }) => [styles.requestBtn, pressed && { opacity: 0.7 }]}
          accessibilityRole="button"
        >
          <Text style={styles.requestBtnText}>Request a spot</Text>
        </Pressable>
      ) : !drawLoading && !scheduleOk?.canRequest && scheduleOk?.cannotRequestReason ? (
        <Text style={styles.tileUnavailable}>{scheduleOk.cannotRequestReason}</Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  scroll: { padding: spacing.lg, gap: spacing.md, flexGrow: 1 },
  heading: { fontSize: 22, fontWeight: '700', color: colors.text },
  toast: {
    padding: spacing.sm,
    borderRadius: radius.md,
    backgroundColor: '#f0fdf4',
    borderWidth: 1,
    borderColor: '#bbf7d0',
  },
  toastText: { fontSize: 13, color: '#166534', fontWeight: '500' },
  tile: {
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.xs,
  },
  tileHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start' },
  tileDay: { fontSize: 11, fontWeight: '700', color: colors.textMuted, textTransform: 'uppercase', letterSpacing: 0.5 },
  tileDate: { fontSize: 11, color: colors.textMuted, marginTop: 1 },
  tileBadge: { fontSize: 13, fontWeight: '600', color: colors.text },
  tileSlot: { fontSize: 13, color: colors.text, fontWeight: '500' },
  tileSchedule: { fontSize: 12, color: colors.textMuted },
  tileUnavailable: { fontSize: 12, color: colors.textMuted, fontStyle: 'italic' },
  tileActions: { gap: spacing.xs, marginTop: spacing.xs },
  requestBtn: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    marginTop: spacing.xs,
  },
  requestBtnText: { color: colors.primaryText, fontWeight: '700', fontSize: 14 },
  confirmBtn: {
    backgroundColor: '#15803d',
    borderRadius: radius.sm,
    paddingVertical: spacing.xs,
    alignItems: 'center',
  },
  confirmBtnText: { color: '#fff', fontWeight: '600', fontSize: 13 },
  cancelBtn: {
    borderRadius: radius.sm,
    paddingVertical: spacing.xs,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#b91c1c',
    backgroundColor: colors.cardBackground,
  },
  cancelBtnText: { color: '#b91c1c', fontWeight: '600', fontSize: 13 },
  detailsLink: { fontSize: 13, color: colors.primary, textDecorationLine: 'underline' },
  historyLink: {
    alignSelf: 'center',
    paddingVertical: spacing.sm,
  },
  historyLinkText: { fontSize: 13, color: colors.primary, fontWeight: '500', textDecorationLine: 'underline' },
});
