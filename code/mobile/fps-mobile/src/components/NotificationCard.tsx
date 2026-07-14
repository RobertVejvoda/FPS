import { Pressable, StyleSheet, Text, View } from 'react-native';
import type { NotificationItem } from '@/api/notifications';
import { displayLocation } from '@/displayLabels';
import { t, tDynamic } from '@/i18n';
import { formatDateTime as formatLocaleDateTime } from '@/i18n/formatters';
import { colors, radius, spacing, touchTarget } from '@/theme';

function typeLabel(notificationType: string): string {
  return tDynamic('labels.notificationType', notificationType, notificationType);
}

const TYPE_BADGE_COLOR: Record<string, string> = {
  RequestSubmitted: colors.primary,
  RequestRejected: colors.danger,
  SlotAllocated: colors.success,
  SlotAllocatedByReallocation: colors.success,
  RequestCancelledBeforeAllocation: colors.textMuted,
  AllocatedReservationCancelled: colors.textMuted,
  LateCancellationPenaltyApplied: colors.warningStrong,
  NoShowRecorded: colors.warningStrong,
  NoShowPenaltyApplied: colors.danger,
  ManualCorrection: '#6d28d9',
  DrawCompleted: colors.primary,
};

function formatDateTime(iso: string): string {
  return formatLocaleDateTime(new Date(iso), {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

type NotificationCardProps = {
  notification: NotificationItem;
  onMarkRead?: () => void;
  testID?: string;
};

export function NotificationCard({ notification, onMarkRead, testID }: NotificationCardProps) {
  const label = typeLabel(notification.notificationType);
  const badgeColor = TYPE_BADGE_COLOR[notification.notificationType] ?? colors.textMuted;
  const locationLabel = displayLocation(notification.locationId);

  return (
    <View
      style={[styles.card, !notification.isRead && styles.cardUnread]}
      testID={testID ?? `notification-card-${notification.id}`}
    >
      <View style={styles.header}>
        <View style={[styles.badge, { backgroundColor: badgeColor }]}>
          <Text style={styles.badgeText}>{label}</Text>
        </View>
        <Text style={styles.time}>{formatDateTime(notification.createdAt)}</Text>
      </View>

      <Text style={styles.message}>{notification.messageText}</Text>

      {notification.relatedDate ? (
        <Text style={styles.detail}>
          {notification.relatedDate}
          {notification.relatedTimeSlot ? `  ·  ${notification.relatedTimeSlot}` : ''}
          {locationLabel ? `  ·  ${locationLabel}` : ''}
        </Text>
      ) : null}

      {notification.nextAction ? (
        <Text style={styles.nextAction}>{notification.nextAction}</Text>
      ) : null}

      {!notification.isRead && onMarkRead ? (
        <Pressable
          accessibilityRole="button"
          onPress={onMarkRead}
          style={({ pressed }) => [styles.markRead, pressed && styles.markReadPressed]}
          testID={`mark-read-${notification.id}`}
        >
          <Text style={styles.markReadText}>{t('notifications.markAsRead')}</Text>
        </Pressable>
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
  cardUnread: {
    borderLeftWidth: 3,
    borderLeftColor: colors.primary,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing.sm,
  },
  badge: {
    paddingVertical: 2,
    paddingHorizontal: spacing.sm,
    borderRadius: radius.sm,
    flexShrink: 1,
  },
  badgeText: {
    fontSize: 11,
    fontWeight: '600',
    color: '#ffffff',
    letterSpacing: 0.3,
  },
  time: {
    fontSize: 12,
    color: colors.textMuted,
    flexShrink: 0,
  },
  message: {
    fontSize: 14,
    color: colors.text,
    lineHeight: 20,
  },
  detail: {
    fontSize: 12,
    color: colors.textMuted,
  },
  nextAction: {
    fontSize: 12,
    color: colors.primary,
    fontWeight: '500',
  },
  markRead: {
    alignSelf: 'flex-start',
    marginTop: spacing.xs,
    minHeight: touchTarget,
    justifyContent: 'center',
    paddingVertical: spacing.xs,
    paddingHorizontal: spacing.md,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.primary,
  },
  markReadPressed: { opacity: 0.6 },
  markReadText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.primary,
  },
});
