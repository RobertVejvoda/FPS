import { useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { colors, radius, spacing } from '@/theme';

const STATUS_LABEL: Record<string, string> = {
  Submitted: 'Waiting for allocation',
  Allocated: 'Parking slot allocated',
  Rejected: 'Request not fulfilled',
  Cancelled: 'Cancelled',
  Expired: 'Time slot has passed',
  Waitlisted: 'Waiting for a released slot',
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

export default function BookingDetailRoute() {
  const params = useLocalSearchParams<{
    requestId: string;
    requestedDate: string;
    timeSlotStart: string;
    timeSlotEnd: string;
    locationId?: string;
    status: string;
    reason?: string;
    allocatedSlotId?: string;
    createdAt: string;
    lastStatusChangedAt: string;
  }>();

  const statusLabel = STATUS_LABEL[params.status] ?? params.status;
  const statusColor = STATUS_COLOR[params.status] ?? colors.textMuted;

  return (
    <SafeAreaView style={styles.safe} edges={['bottom']}>
      <ScrollView contentContainerStyle={styles.container}>

        {/* Status */}
        <View style={[styles.statusBanner, { borderLeftColor: statusColor }]}>
          <Text style={[styles.statusText, { color: statusColor }]}>{statusLabel}</Text>
          <View style={[styles.statusBadge, { backgroundColor: statusColor }]}>
            <Text style={styles.statusBadgeText}>{params.status}</Text>
          </View>
        </View>

        {/* Reason */}
        {params.reason ? (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Reason</Text>
            <View style={styles.card}>
              <Text style={styles.reasonText}>{params.reason}</Text>
            </View>
          </View>
        ) : null}

        {/* Booking info */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Request</Text>
          <View style={styles.card}>
            <Row label="Date" value={formatDate(params.requestedDate)} />
            <Row label="Time" value={`${formatTime(params.timeSlotStart)} – ${formatTime(params.timeSlotEnd)}`} />
            {params.locationId ? <Row label="Location" value={params.locationId} /> : null}
            {params.allocatedSlotId ? <Row label="Allocated slot" value={params.allocatedSlotId} /> : null}
          </View>
        </View>

        {/* Timeline */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Timeline</Text>
          <View style={styles.card}>
            <Row label="Submitted" value={formatDateTime(params.createdAt)} />
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
  reasonText: { fontSize: 15, color: colors.text, lineHeight: 22 },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md },
  rowLabel: { fontSize: 14, color: colors.textMuted, flexShrink: 0 },
  rowValue: { fontSize: 14, color: colors.text, fontWeight: '500', textAlign: 'right', flex: 1 },
});
