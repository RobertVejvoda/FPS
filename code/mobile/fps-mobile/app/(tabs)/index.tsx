import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuth } from '@/auth/AuthContext';
import { useBookings } from '@/api/useBookings';
import { cancelBooking, confirmBookingUsage, type BookingListItem } from '@/api/bookings';
import { fetchDrawStatus, type DrawStatusResult } from '@/api/draws';
import { StateView } from '@/components/StateView';
import { displayModule, displaySlot, isSeatsItem, statusBadgeLabel, formatCutOffAt } from '@/displayLabels';
import { DEMO_LOCATION_ID, DEFAULT_TIME_SLOT_START, DEFAULT_TIME_SLOT_END } from '@/demoDefaults';
import { t } from '@/i18n';
import { formatDate as formatLocaleDate } from '@/i18n/formatters';
import { colors, radius, spacing } from '@/theme';

function dateStr(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function isWorkday(d: Date): boolean {
  const day = d.getDay();
  return day >= 1 && day <= 5;
}

// Four day focus cards: today, tomorrow, then the next two working days
// (weekends skipped), matching the web My Reservations model. Labels use Today/Tomorrow
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
      const label = offset === 0 ? t('common.today')
        : offset === 1 ? t('common.tomorrow')
        : formatLocaleDate(cand, { weekday: 'long' });
      out.push({ label, date: dateStr(cand), offset });
    }
    cand.setDate(cand.getDate() + 1);
  }
  return out;
}

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
      resourceType: item.resourceType ?? 'Parking',
    },
  };
}

export default function HomeRoute() {
  const router = useRouter();
  const { apiBaseUrl, bearerToken, clearSession } = useAuth();
  const { state, refresh } = useBookings('all');
  // Recomputed once per component instance — a fresh instance is created
  // whenever the locale changes (LocaleProvider remounts the tree), so
  // Today/Tomorrow/weekday labels stay in sync with the active language.
  const DAYS = useMemo(() => workdayCards(4), []);
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
    Alert.alert(t('booking.dialog.cancelTitle'), t('booking.dialog.cancelMessage'), [
      { text: t('booking.dialog.keepIt'), style: 'cancel' },
      {
        text: t('booking.dialog.cancelTitle'), style: 'destructive',
        onPress: async () => {
          setBusyId(requestId);
          const result = await cancelBooking({ apiBaseUrl, bearerToken }, requestId);
          setBusyId(null);
          if (result.kind === 'ok') { showToast(t('booking.toast.cancelled')); refresh(); }
          else if (result.kind === 'unauthenticated') { await clearSession(); router.replace('/login'); }
          else showToast('message' in result ? result.message : t('booking.toast.couldNotCancel'));
        },
      },
    ]);
  }, [apiBaseUrl, bearerToken, clearSession, refresh, router]);

  const handleConfirm = useCallback(async (requestId: string) => {
    setBusyId(requestId);
    const result = await confirmBookingUsage({ apiBaseUrl, bearerToken }, requestId);
    setBusyId(null);
    if (result.kind === 'confirmed') {
      showToast(result.wasAlreadyConfirmed ? t('booking.toast.usageAlreadyRecorded') : t('booking.toast.usageConfirmed'));
      refresh();
    } else if (result.kind === 'unauthenticated') {
      await clearSession();
      router.replace('/login');
    } else {
      showToast('message' in result ? result.message : t('booking.toast.couldNotConfirm'));
    }
  }, [apiBaseUrl, bearerToken, clearSession, refresh, router]);

  const handleUnauthenticated = useCallback(async () => {
    await clearSession();
    router.replace('/login');
  }, [clearSession, router]);

  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <SafeAreaView style={styles.safe}>
        <StateView kind="loading" title={t('booking.home.loading')} />
      </SafeAreaView>
    );
  }

  if (state.kind === 'unauthenticated') {
    return (
      <SafeAreaView style={styles.safe}>
        <StateView
          kind="unauthenticated"
          title={t('session.notSignedIn')}
          message={t('session.expiredMessage')}
          actionLabel={t('session.signIn')}
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
          title={t('booking.home.cannotLoad')}
          message={t('common.checkConnection')}
          actionLabel={t('common.retry')}
          onAction={refresh}
        />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <Text style={styles.heading}>{t('booking.myReservations')}</Text>

        {toastMsg && (
          <View style={styles.toast}>
            <Text style={styles.toastText}>{toastMsg}</Text>
          </View>
        )}

        {/* Three day tiles */}
        {DAYS.map((day, i) => {
          const date = day.date;
          // Parking is the fully wired request module for the day cards; a seat
          // reservation for the same day renders as a compact badged row (UX008 #781).
          const booking = state.items.find(b => b.requestedDate === date && !isSeatsItem(b)) ?? null;
          const seatBooking = state.items.find(b => b.requestedDate === date && isSeatsItem(b)) ?? null;
          const drawStatus = drawStatuses[i] ?? null;
          return (
            <DayTile
              key={day.offset}
              label={day.label}
              date={date}
              booking={booking}
              seatBooking={seatBooking}
              drawStatus={drawStatus}
              drawLoading={drawLoading}
              busy={busyId === booking?.requestId}
              onCancel={booking?.nextAction === 'cancel' ? () => handleCancel(booking.requestId) : undefined}
              onConfirm={booking?.nextAction === 'confirmUsage' ? () => handleConfirm(booking.requestId) : undefined}
              onRequest={() => router.push({ pathname: '/(tabs)/new', params: { offset: String(day.offset) } })}
              onDetails={booking ? () => router.push(bookingParams(booking)) : undefined}
              onSeatDetails={seatBooking ? () => router.push(bookingParams(seatBooking)) : undefined}
            />
          );
        })}

        {/* Secondary navigation */}
        <Pressable
          onPress={() => router.push('/(tabs)/bookings')}
          style={({ pressed }) => [styles.historyLink, pressed && { opacity: 0.6 }]}
          accessibilityRole="button"
        >
          <Text style={styles.historyLinkText}>{t('booking.home.historyLink')}</Text>
        </Pressable>
      </ScrollView>
    </SafeAreaView>
  );
}

function DayTile({ label, date, booking, seatBooking, drawStatus, drawLoading, busy, onCancel, onConfirm, onRequest, onDetails, onSeatDetails }: {
  label: string;
  date: string;
  booking: BookingListItem | null;
  seatBooking: BookingListItem | null;
  drawStatus: DrawStatusResult | null;
  drawLoading: boolean;
  busy: boolean;
  onCancel?: () => void;
  onConfirm?: () => void;
  onRequest?: () => void;
  onDetails?: () => void;
  onSeatDetails?: () => void;
}) {
  const scheduleOk = drawStatus?.kind === 'ok' ? drawStatus.data : null;
  const badgeLabel = booking ? statusBadgeLabel(booking.status) : null;
  const slot = booking ? displaySlot(booking.allocatedSlotId) : null;
  const d = new Date(date + 'T00:00:00');
  const dateLabel = formatLocaleDate(d, { month: 'short', day: 'numeric' });

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
      {slot && <Text style={styles.tileSlot}>{t('booking.tile.spot', { slot })}</Text>}

      {/* Seat reservation for the day — compact badged row, only when one exists. */}
      {seatBooking && (
        <Pressable onPress={onSeatDetails} accessibilityRole="button" style={({ pressed }) => [styles.tileSeatRow, pressed && { opacity: 0.7 }]}>
          <Text style={styles.tileSeatBadge}>{displayModule(seatBooking.resourceType)}</Text>
          <Text style={styles.tileSeatStatus}>{statusBadgeLabel(seatBooking.status)}</Text>
          {displaySlot(seatBooking.allocatedSlotId) ? (
            <Text style={styles.tileSeatLabel}>{displaySlot(seatBooking.allocatedSlotId)}</Text>
          ) : null}
        </Pressable>
      )}

      {/* Schedule timing */}
      {drawLoading && <ActivityIndicator size="small" color={colors.primary} style={{ marginTop: spacing.xs }} />}
      {!drawLoading && scheduleOk?.nextDrawAt && (
        <Text style={styles.tileSchedule}>{t('booking.tile.draw', { time: formatCutOffAt(scheduleOk.nextDrawAt, scheduleOk.timeZone) })}</Text>
      )}
      {!drawLoading && scheduleOk?.cutOffAt && (
        <Text style={styles.tileSchedule}>{t('booking.tile.cutoff', { time: formatCutOffAt(scheduleOk.cutOffAt, scheduleOk.timeZone) })}</Text>
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
              <Text style={styles.confirmBtnText}>{busy ? t('booking.tile.confirming') : t('booking.confirmUsage')}</Text>
            </Pressable>
          )}
          {onCancel && (
            <Pressable
              onPress={onCancel}
              disabled={busy}
              style={({ pressed }) => [styles.cancelBtn, (busy || pressed) && { opacity: 0.6 }]}
              accessibilityRole="button"
            >
              <Text style={styles.cancelBtnText}>{busy ? t('booking.tile.cancelling') : t('common.cancel')}</Text>
            </Pressable>
          )}
          {!onCancel && !onConfirm && onDetails && (
            <Pressable onPress={onDetails} accessibilityRole="button">
              <Text style={styles.detailsLink}>{t('booking.tile.viewDetails')}</Text>
            </Pressable>
          )}
        </View>
      ) : !drawLoading && scheduleOk?.canRequest ? (
        <Pressable
          onPress={onRequest}
          style={({ pressed }) => [styles.requestBtn, pressed && { opacity: 0.7 }]}
          accessibilityRole="button"
        >
          <Text style={styles.requestBtnText}>{t('booking.tile.requestSpot')}</Text>
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
  tileSeatRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
    marginTop: 2,
    paddingVertical: 4,
    paddingHorizontal: 6,
    borderWidth: 1,
    borderStyle: 'dashed',
    borderColor: colors.border,
    borderRadius: radius.sm,
    alignSelf: 'flex-start',
  },
  tileSeatBadge: {
    fontSize: 10,
    fontWeight: '700',
    color: '#166534',
    backgroundColor: '#ecfdf5',
    borderRadius: 999,
    paddingHorizontal: 6,
    paddingVertical: 1,
    overflow: 'hidden',
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  tileSeatStatus: { fontSize: 12, fontWeight: '600', color: colors.text },
  tileSeatLabel: { fontSize: 12, color: colors.textMuted },
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
