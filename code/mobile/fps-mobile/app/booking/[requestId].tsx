import { useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { fetchDrawStatus, type DrawStatusResponse } from '@/api/draws';
import { useAuth } from '@/auth/AuthContext';
import { displayLocation, displayModule, displayNextDrawRun, displayResourceNoun, displayResourceNounPlural, displaySlot, humanizeRejectionReason, shouldShowNextDraw, statusBadgeLabel, demandShortLabel } from '@/displayLabels';
import { t, tDynamic } from '@/i18n';
import { formatDate as formatLocaleDate, formatDateTime as formatLocaleDateTime, formatWallClock } from '@/i18n/formatters';
import { colors, radius, spacing } from '@/theme';

// UX008 (#781) — module-aware status meaning: allocated/waitlisted copy names the
// module's resource (spot vs seat) instead of assuming parking.
function statusLabelFor(status: string, resourceType?: string | null): string {
  const noun = displayResourceNoun(resourceType).toLowerCase();
  if (status === 'Allocated') {
    return resourceType === 'Seats' ? t('booking.detail.statusLong.AllocatedSeat') : t('booking.detail.statusLong.AllocatedSpot');
  }
  if (status === 'Waitlisted') {
    return t('booking.detail.statusLong.Waitlisted', { noun });
  }
  return tDynamic('booking.detail.statusLong', status, status);
}

const STATUS_COLOR: Record<string, string> = {
  Submitted: colors.primary,
  Allocated: '#15803d',
  Rejected: colors.danger,
  Cancelled: colors.textMuted,
  Expired: colors.textMuted,
  Waitlisted: '#92400e',
  UsageConfirmed: '#15803d',
  NoShow: '#b45309',
  Pending: colors.primary,
};

function formatDate(dateStr: string): string {
  const [y, m, d] = dateStr.split('-').map(Number);
  return formatLocaleDate(new Date(y, m - 1, d), {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

function formatTime(timeStr: string): string {
  const [h, m] = timeStr.split(':');
  return formatWallClock(parseInt(h, 10), parseInt(m, 10));
}

function formatDateTime(iso: string): string {
  return formatLocaleDateTime(new Date(iso), {
    month: 'short', day: 'numeric', year: 'numeric',
    hour: 'numeric', minute: '2-digit',
  });
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

function AllocationExplanation({
  status,
  reasonCode,
  reason,
  draw,
  nextDrawLabel,
  resourceType,
}: {
  status: string;
  reasonCode: string | undefined;
  reason: string | undefined;
  draw: DrawStatusResponse | null;
  nextDrawLabel: string | null;
  resourceType: string | undefined;
}) {
  const isPreDraw = shouldShowNextDraw(status);
  const isCompleted = draw?.status === 'Completed';
  const isDrawCapacityRejection = reasonCode === 'DrawNotSelected' || (!reasonCode && isCompleted);
  const nounPlural = displayResourceNounPlural(resourceType);
  const availableLabel = t('booking.detail.available', { noun: nounPlural });

  if (!isPreDraw && status !== 'Allocated' && status !== 'Rejected') return null;

  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{t('booking.detail.section.allocationExplanation')}</Text>
      <View style={styles.card}>
        {isPreDraw && (
          <>
            {nextDrawLabel ? <Row label={t('booking.detail.nextDraw')} value={nextDrawLabel} /> : null}
            {draw ? (
              <>
                <Row label={t('booking.detail.demandSoFar')} value={demandShortLabel(draw.demandLevel)} />
                <Row label={t('booking.detail.requestsSoFar')} value={String(draw.requestCount)} />
                {Number(draw.availableSpotCount) > 0 ? (
                  <Row label={availableLabel} value={String(draw.availableSpotCount)} />
                ) : null}
              </>
            ) : null}
            <Text style={styles.explanationText}>
              {t('booking.detail.eligibleMessage')}
            </Text>
          </>
        )}

        {status === 'Allocated' && (
          <>
            {isCompleted && draw?.completedAt ? (
              <Row label={t('booking.detail.drawCompleted')} value={formatDateTime(draw.completedAt)} />
            ) : null}
            {draw ? (
              <>
                <Row label={t('booking.detail.demand')} value={demandShortLabel(draw.demandLevel)} />
                <Row label={t('booking.detail.requests')} value={String(draw.requestCount)} />
                {Number(draw.availableSpotCount) > 0 ? (
                  <Row label={availableLabel} value={String(draw.availableSpotCount)} />
                ) : null}
              </>
            ) : null}
            <Text style={[styles.resultLabel, { color: '#15803d' }]}>{t('booking.detail.resultAllocated')}</Text>
            <Text style={styles.explanationText}>
              {resourceType === 'Seats'
                ? t('booking.detail.matchedSeat')
                : t('booking.detail.matchedSpot')}
            </Text>
          </>
        )}

        {status === 'Rejected' && (
          <>
            {isDrawCapacityRejection && draw?.completedAt ? (
              <Row label={t('booking.detail.drawCompleted')} value={formatDateTime(draw.completedAt)} />
            ) : null}
            {isDrawCapacityRejection && draw ? (
              <>
                <Row label={t('booking.detail.demand')} value={demandShortLabel(draw.demandLevel)} />
                <Row label={t('booking.detail.requests')} value={String(draw.requestCount)} />
                {Number(draw.availableSpotCount) > 0 ? (
                  <Row label={availableLabel} value={String(draw.availableSpotCount)} />
                ) : null}
              </>
            ) : null}
            <Text style={[styles.resultLabel, { color: colors.danger }]}>{t('booking.detail.resultNotAllocated')}</Text>
            <Text style={styles.explanationText}>
              {isDrawCapacityRejection
                ? t('booking.detail.moreThanAvailable', { noun: nounPlural })
                : humanizeRejectionReason(reasonCode ?? null, reason ?? null)}
            </Text>
          </>
        )}
      </View>
    </View>
  );
}

export default function BookingDetailRoute() {
  const params = useLocalSearchParams<{
    requestId: string;
    requestedDate: string;
    timeSlotStart: string;
    timeSlotEnd: string;
    locationId?: string;
    status: string;
    reason?: string;
    reasonCode?: string;
    allocatedSlotId?: string;
    createdAt: string;
    lastStatusChangedAt: string;
    resourceType?: string;
  }>();

  const { apiBaseUrl, bearerToken } = useAuth();
  const [drawStatus, setDrawStatus] = useState<DrawStatusResponse | null>(null);

  const needsDraw = shouldShowNextDraw(params.status)
    || params.status === 'Allocated'
    || params.status === 'Rejected';

  useEffect(() => {
    if (!needsDraw || !params.locationId) return;
    fetchDrawStatus({ apiBaseUrl, bearerToken }, {
      date: params.requestedDate,
      locationId: params.locationId,
      timeSlotStart: params.timeSlotStart,
      timeSlotEnd: params.timeSlotEnd,
    }).then((res) => {
      if (res.kind === 'ok') setDrawStatus(res.data);
    });
  }, [needsDraw, params.requestedDate, params.locationId, params.timeSlotStart, params.timeSlotEnd, apiBaseUrl, bearerToken]);

  const statusLabel = statusLabelFor(params.status, params.resourceType);
  const statusColor = STATUS_COLOR[params.status] ?? colors.textMuted;
  const locationLabel = displayLocation(params.locationId);
  const slotLabel = displaySlot(params.allocatedSlotId);
  const nextDrawLabel = shouldShowNextDraw(params.status) ? displayNextDrawRun(params.requestedDate) : null;

  return (
    <SafeAreaView style={styles.safe} edges={['bottom']}>
      <ScrollView contentContainerStyle={styles.container}>

        {/* Status */}
        <View style={[styles.statusBanner, { borderLeftColor: statusColor }]}>
          <Text style={[styles.statusText, { color: statusColor }]}>{statusLabel}</Text>
          <View style={[styles.statusBadge, { backgroundColor: statusColor }]}>
            <Text style={styles.statusBadgeText}>{statusBadgeLabel(params.status)}</Text>
          </View>
        </View>

        {/* Rejection note — only for non-draw-explained rejections */}
        {params.status !== 'Rejected' && params.reason ? (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>{t('booking.detail.note')}</Text>
            <View style={styles.card}>
              <Text style={styles.explanationText}>{params.reason}</Text>
            </View>
          </View>
        ) : null}

        {/* Allocation explanation — pre-draw and post-draw */}
        <AllocationExplanation
          status={params.status}
          reasonCode={params.reasonCode}
          reason={params.reason}
          draw={drawStatus}
          nextDrawLabel={nextDrawLabel}
          resourceType={params.resourceType}
        />

        {/* Booking info */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>{t('booking.detail.request')}</Text>
          <View style={styles.card}>
            <Row label={t('booking.field.date')} value={formatDate(params.requestedDate)} />
            <Row label={t('booking.field.time')} value={`${formatTime(params.timeSlotStart)} – ${formatTime(params.timeSlotEnd)}`} />
            {params.resourceType === 'Seats' ? <Row label={t('booking.detail.module')} value={displayModule(params.resourceType)} /> : null}
            {locationLabel ? <Row label={t('booking.field.location')} value={locationLabel} /> : null}
            {slotLabel ? <Row label={t('booking.detail.allocatedNoun', { noun: displayResourceNoun(params.resourceType).toLowerCase() })} value={slotLabel} /> : null}
          </View>
        </View>

        {/* Timeline */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>{t('booking.detail.timeline')}</Text>
          <View style={styles.card}>
            <Row label={t('booking.detail.submittedAt')} value={formatDateTime(params.createdAt)} />
            {nextDrawLabel ? <Row label={t('booking.detail.nextDraw')} value={nextDrawLabel} /> : null}
            {drawStatus?.completedAt && !shouldShowNextDraw(params.status) ? (
              <Row label={t('booking.detail.drawCompleted')} value={formatDateTime(drawStatus.completedAt)} />
            ) : null}
            <Row label={t('booking.detail.lastUpdated')} value={formatDateTime(params.lastStatusChangedAt)} />
          </View>
        </View>

      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  container: { padding: spacing.lg, gap: spacing.lg },
  statusBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.cardBackground,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    borderLeftWidth: 4,
    gap: spacing.sm,
  },
  statusText: { fontSize: 15, fontWeight: '600', flex: 1 },
  statusBadge: { paddingVertical: 2, paddingHorizontal: spacing.sm, borderRadius: radius.sm },
  statusBadgeText: { fontSize: 11, fontWeight: '600', color: '#ffffff' },
  section: { gap: spacing.sm },
  sectionTitle: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.textMuted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  card: {
    backgroundColor: colors.cardBackground,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    gap: spacing.sm,
  },
  explanationText: { fontSize: 15, color: colors.text, lineHeight: 22 },
  resultLabel: { fontSize: 15, fontWeight: '600' },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md },
  rowLabel: { fontSize: 14, color: colors.textMuted, flexShrink: 0 },
  rowValue: { fontSize: 14, color: colors.text, fontWeight: '500', textAlign: 'right', flex: 1 },
});
