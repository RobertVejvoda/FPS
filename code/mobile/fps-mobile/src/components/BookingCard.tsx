import { StyleSheet, Text, View } from 'react-native';
import type { BookingListItem } from '@/api/bookings';
import { colors, radius, spacing } from '@/theme';

type BookingCardProps = {
  booking: BookingListItem;
  testID?: string;
};

const STATUS_BADGE_COLOR: Record<string, string> = {
  Submitted: colors.primary,
  Allocated: '#15803d',
  Rejected: colors.danger,
  Cancelled: colors.textMuted,
  Expired: colors.textMuted,
  Waitlisted: '#92400e',
  UsageConfirmed: '#15803d',
  NoShow: '#b45309',
};

function formatDate(dateStr: string): string {
  const [year, month, day] = dateStr.split('-').map(Number);
  return new Date(year, month - 1, day).toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  });
}

function formatTime(timeStr: string): string {
  const [h, m] = timeStr.split(':');
  const hour = parseInt(h, 10);
  const ampm = hour >= 12 ? 'PM' : 'AM';
  const displayHour = hour % 12 || 12;
  return `${displayHour}:${m.padStart(2, '0')} ${ampm}`;
}

export function BookingCard({ booking, testID }: BookingCardProps) {
  const badgeColor = STATUS_BADGE_COLOR[booking.status] ?? colors.textMuted;

  return (
    <View style={styles.card} testID={testID ?? `booking-card-${booking.requestId}`}>
      <View style={styles.header}>
        <Text style={styles.date}>{formatDate(booking.requestedDate)}</Text>
        <View style={[styles.badge, { backgroundColor: badgeColor }]}>
          <Text style={styles.badgeText}>{booking.status}</Text>
        </View>
      </View>

      <Text style={styles.timeSlot}>
        {formatTime(booking.timeSlotStart)} – {formatTime(booking.timeSlotEnd)}
      </Text>

      {booking.locationId ? (
        <Text style={styles.detail}>Location: {booking.locationId}</Text>
      ) : null}

      {booking.reason ? (
        <Text style={styles.reason}>{booking.reason}</Text>
      ) : null}

      {booking.nextAction && booking.nextAction !== 'None' ? (
        <Text style={styles.nextAction}>{booking.nextAction}</Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
    gap: spacing.xs,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  date: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text,
  },
  badge: {
    paddingVertical: 2,
    paddingHorizontal: spacing.sm,
    borderRadius: radius.sm,
  },
  badgeText: {
    fontSize: 11,
    fontWeight: '600',
    color: '#ffffff',
    letterSpacing: 0.3,
  },
  timeSlot: {
    fontSize: 13,
    color: colors.textMuted,
  },
  detail: {
    fontSize: 13,
    color: colors.textMuted,
  },
  reason: {
    fontSize: 13,
    color: colors.text,
    fontStyle: 'italic',
  },
  nextAction: {
    fontSize: 12,
    color: colors.primary,
    fontWeight: '500',
  },
});
