import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import type { BookingListItem } from '@/api/bookings';
import { colors, radius, spacing } from '@/theme';

type BookingCardProps = {
  booking: BookingListItem;
  testID?: string;
  onCancel?: () => void;
  onConfirmUsage?: () => void;
  actionPending?: boolean;
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

const NEXT_ACTION_LABEL: Record<string, string> = {
  cancel: 'Cancel booking',
  confirmUsage: 'Confirm usage',
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

export function BookingCard({ booking, testID, onCancel, onConfirmUsage, actionPending }: BookingCardProps) {
  const badgeColor = STATUS_BADGE_COLOR[booking.status] ?? colors.textMuted;
  const nextAction =
    booking.nextAction && booking.nextAction.toLowerCase() !== 'none'
      ? booking.nextAction
      : null;

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

      {booking.allocatedSlotId ? (
        <Text style={styles.detail}>Slot: {booking.allocatedSlotId}</Text>
      ) : null}

      {booking.reason ? (
        <Text style={styles.reason}>{booking.reason}</Text>
      ) : null}

      {nextAction === 'cancel' && onCancel ? (
        <Pressable
          onPress={onCancel}
          disabled={actionPending}
          accessibilityRole="button"
          style={({ pressed }) => [styles.actionButton, styles.cancelButton, (pressed || actionPending) && styles.actionButtonDimmed]}
          testID={`cancel-${booking.requestId}`}
        >
          {actionPending
            ? <ActivityIndicator size="small" color="#ffffff" />
            : <Text style={styles.actionButtonText}>{NEXT_ACTION_LABEL.cancel}</Text>}
        </Pressable>
      ) : nextAction === 'confirmUsage' && onConfirmUsage ? (
        <Pressable
          onPress={onConfirmUsage}
          disabled={actionPending}
          accessibilityRole="button"
          style={({ pressed }) => [styles.actionButton, styles.confirmButton, (pressed || actionPending) && styles.actionButtonDimmed]}
          testID={`confirm-${booking.requestId}`}
        >
          {actionPending
            ? <ActivityIndicator size="small" color="#ffffff" />
            : <Text style={styles.actionButtonText}>{NEXT_ACTION_LABEL.confirmUsage}</Text>}
        </Pressable>
      ) : nextAction ? (
        <Text style={styles.nextAction}>{NEXT_ACTION_LABEL[nextAction] ?? nextAction}</Text>
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
  actionButton: {
    marginTop: spacing.xs,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderRadius: radius.sm,
    alignItems: 'center',
    minHeight: 36,
    justifyContent: 'center',
  },
  cancelButton: {
    backgroundColor: colors.danger,
  },
  confirmButton: {
    backgroundColor: '#15803d',
  },
  actionButtonDimmed: {
    opacity: 0.55,
  },
  actionButtonText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#ffffff',
  },
});
