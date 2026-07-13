import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import type { BookingListItem } from '@/api/bookings';
import { displayLocation, displayModule, displayNextDrawRun, displayResourceNoun, displaySlot, humanizeRejectionReason, isSeatsItem, shouldShowNextDraw, STATUS_BADGE_LABEL } from '@/displayLabels';
import { colors, radius, spacing } from '@/theme';

type BookingCardProps = {
  booking: BookingListItem;
  testID?: string;
  onPress?: () => void;
  onCancel?: () => void;
  onConfirmUsage?: () => void;
  actionPending?: boolean;
  // UX008 (#781) — render a module badge when the surrounding list spans modules.
  showModule?: boolean;
};

const STATUS_BADGE_COLOR: Record<string, string> = {
  Submitted: colors.primary,
  Pending: colors.primary,
  Allocated: '#15803d',
  Rejected: colors.danger,
  Cancelled: colors.textMuted,
  Expired: colors.textMuted,
  Waitlisted: '#92400e',
  UsageConfirmed: '#15803d',
  NoShow: '#b45309',
};

const NEXT_ACTION_LABEL: Record<string, string> = {
  cancel: 'Cancel request',
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

export function BookingCard({ booking, testID, onPress, onCancel, onConfirmUsage, actionPending, showModule }: BookingCardProps) {
  const badgeColor = STATUS_BADGE_COLOR[booking.status] ?? colors.textMuted;
  const nextAction =
    booking.nextAction && booking.nextAction.toLowerCase() !== 'none'
      ? booking.nextAction
      : null;
  const locationLabel = displayLocation(booking.locationId);
  const slotLabel = displaySlot(booking.allocatedSlotId);
  const nextDrawLabel = shouldShowNextDraw(booking.status) ? displayNextDrawRun(booking.requestedDate) : null;

  const badgeLabel = STATUS_BADGE_LABEL[booking.status] ?? booking.status;
  const rejectionReason = booking.status === 'Rejected'
    ? humanizeRejectionReason(booking.reasonCode ?? null, booking.reason ?? null)
    : null;

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole={onPress ? 'button' : 'none'}
      style={({ pressed }) => [pressed && onPress ? { opacity: 0.85 } : undefined]}
      testID={testID ?? `booking-card-${booking.requestId}`}
    >
    <View style={styles.card}>
      <View style={styles.header}>
        <View style={styles.headerLeft}>
          <Text style={styles.date}>{formatDate(booking.requestedDate)}</Text>
          {showModule ? (
            <Text style={[styles.moduleBadge, isSeatsItem(booking) && styles.moduleBadgeSeats]}>
              {displayModule(booking.resourceType)}
            </Text>
          ) : null}
        </View>
        <View style={[styles.badge, { backgroundColor: badgeColor }]}>
          <Text style={styles.badgeText}>{badgeLabel}</Text>
        </View>
      </View>

      <Text style={styles.timeSlot}>
        {formatTime(booking.timeSlotStart)} – {formatTime(booking.timeSlotEnd)}
      </Text>

      {locationLabel ? (
        <Text style={styles.detail}>Location: {locationLabel}</Text>
      ) : null}

      {slotLabel ? (
        <Text style={styles.detail}>{displayResourceNoun(booking.resourceType)}: {slotLabel}</Text>
      ) : null}

      {nextDrawLabel ? (
        <Text style={styles.nextDraw}>Waiting for allocation. Next draw: {nextDrawLabel}</Text>
      ) : null}

      {rejectionReason ? (
        <Text style={styles.reason}>{rejectionReason}</Text>
      ) : booking.status !== 'Rejected' && booking.reason ? (
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
    </Pressable>
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
  headerLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
    flexShrink: 1,
  },
  moduleBadge: {
    fontSize: 10,
    fontWeight: '700',
    color: '#374151',
    backgroundColor: '#eef2f7',
    borderRadius: 999,
    paddingHorizontal: 6,
    paddingVertical: 1,
    overflow: 'hidden',
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  moduleBadgeSeats: {
    color: '#166534',
    backgroundColor: '#ecfdf5',
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
  nextDraw: {
    fontSize: 13,
    color: colors.primary,
    fontWeight: '600',
  },
  actionButton: {
    marginTop: spacing.xs,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderRadius: radius.sm,
    alignItems: 'center',
    minHeight: 44,
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
