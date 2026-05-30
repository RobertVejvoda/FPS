import { useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { fetchDrawStatus, type DrawStatusResponse } from '@/api/draws';
import { useAuth } from '@/auth/AuthContext';
import { displayLocation, displayNextDrawRun, displaySlot, shouldShowNextDraw, STATUS_BADGE_LABEL } from '@/displayLabels';
import { colors, radius, spacing } from '@/theme';

const STATUS_LABEL: Record<string, string> = {
  Submitted: 'Waiting for allocation',
  Allocated: 'Parking spot allocated',
  Rejected: 'Request not fulfilled',
  Cancelled: 'Cancelled',
  Expired: 'Time slot has passed',
  Waitlisted: 'Waiting for a released spot',
  UsageConfirmed: 'Usage confirmed',
  NoShow: 'No-show recorded',
  Pending: 'Pending — draw in progress',
};

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

const DEMAND_LABEL: Record<string, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
};

function formatDate(dateStr: string): string {
  const [y, m, d] = dateStr.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

function formatTime(t: string): string {
  const [h, m] = t.split(':');
  const hour = parseInt(h, 10);
  return `${hour % 12 || 12}:${m.padStart(2, '0')} ${hour >= 12 ? 'PM' : 'AM'}`;
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
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
  draw,
  nextDrawLabel,
}: {
  status: string;
  draw: DrawStatusResponse | null;
  nextDrawLabel: string | null;
}) {
  const isPreDraw = shouldShowNextDraw(status);
  const isCompleted = draw?.status === 'Completed';

  if (!isPreDraw && status !== 'Allocated' && status !== 'Rejected') return null;

  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>Allocation explanation</Text>
      <View style={styles.card}>
        {isPreDraw && (
          <>
            {nextDrawLabel ? <Row label="Next draw" value={nextDrawLabel} /> : null}
            {draw ? (
              <>
                <Row label="Demand so far" value={DEMAND_LABEL[draw.demandLevel] ?? draw.demandLevel} />
                <Row label="Requests so far" value={String(draw.requestCount)} />
                {Number(draw.availableSpotCount) > 0 ? (
                  <Row label="Available spots" value={String(draw.availableSpotCount)} />
                ) : null}
              </>
            ) : null}
            <Text style={styles.explanationText}>
              You are eligible. Final allocation follows eligibility and fairness rules.
            </Text>
          </>
        )}

        {status === 'Allocated' && (
          <>
            {isCompleted && draw?.completedAt ? (
              <Row label="Draw completed" value={formatDateTime(draw.completedAt)} />
            ) : null}
            {draw ? (
              <>
                <Row label="Demand" value={DEMAND_LABEL[draw.demandLevel] ?? draw.demandLevel} />
                <Row label="Requests" value={String(draw.requestCount)} />
                {Number(draw.availableSpotCount) > 0 ? (
                  <Row label="Available spots" value={String(draw.availableSpotCount)} />
                ) : null}
              </>
            ) : null}
            <Text style={[styles.resultLabel, { color: '#15803d' }]}>Result: Allocated</Text>
            <Text style={styles.explanationText}>
              Your request matched an available parking spot.
            </Text>
          </>
        )}

        {status === 'Rejected' && (
          <>
            {draw?.completedAt ? (
              <Row label="Draw completed" value={formatDateTime(draw.completedAt)} />
            ) : null}
            {draw ? (
              <>
                <Row label="Demand" value={DEMAND_LABEL[draw.demandLevel] ?? draw.demandLevel} />
                <Row label="Requests" value={String(draw.requestCount)} />
                {Number(draw.availableSpotCount) > 0 ? (
                  <Row label="Available spots" value={String(draw.availableSpotCount)} />
                ) : null}
              </>
            ) : null}
            <Text style={[styles.resultLabel, { color: colors.danger }]}>Result: Not allocated</Text>
            <Text style={styles.explanationText}>
              More eligible requests than available spots. The draw followed company policy.
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

  const statusLabel = STATUS_LABEL[params.status] ?? params.status;
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
            <Text style={styles.statusBadgeText}>{STATUS_BADGE_LABEL[params.status] ?? params.status}</Text>
          </View>
        </View>

        {/* Rejection note — only for non-draw-explained rejections */}
        {params.status !== 'Rejected' && params.reason ? (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Note</Text>
            <View style={styles.card}>
              <Text style={styles.explanationText}>{params.reason}</Text>
            </View>
          </View>
        ) : null}

        {/* Allocation explanation — pre-draw and post-draw */}
        <AllocationExplanation
          status={params.status}
          draw={drawStatus}
          nextDrawLabel={nextDrawLabel}
        />

        {/* Booking info */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Request</Text>
          <View style={styles.card}>
            <Row label="Date" value={formatDate(params.requestedDate)} />
            <Row label="Time" value={`${formatTime(params.timeSlotStart)} – ${formatTime(params.timeSlotEnd)}`} />
            {locationLabel ? <Row label="Location" value={locationLabel} /> : null}
            {slotLabel ? <Row label="Allocated spot" value={slotLabel} /> : null}
          </View>
        </View>

        {/* Timeline */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Timeline</Text>
          <View style={styles.card}>
            <Row label="Submitted" value={formatDateTime(params.createdAt)} />
            {nextDrawLabel ? <Row label="Next draw" value={nextDrawLabel} /> : null}
            {drawStatus?.completedAt && !shouldShowNextDraw(params.status) ? (
              <Row label="Draw completed" value={formatDateTime(drawStatus.completedAt)} />
            ) : null}
            <Row label="Last updated" value={formatDateTime(params.lastStatusChangedAt)} />
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
