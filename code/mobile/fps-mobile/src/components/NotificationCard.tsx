import { Pressable, StyleSheet, Text, View } from 'react-native';
import type { NotificationDto } from '@/api/notifications';
import { colors, radius, spacing } from '@/theme';

type NotificationCardProps = {
  notification: NotificationDto;
  testID?: string;
  onPress?: () => void;
};

function formatDateTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return date.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined
  });
}

function getNotificationIcon(notificationType: string): string {
  if (notificationType.includes('allocated') || notificationType.includes('Allocated')) return '✓';
  if (notificationType.includes('rejected') || notificationType.includes('Rejected')) return '✗';
  if (notificationType.includes('cancelled') || notificationType.includes('Cancelled')) return '⊗';
  if (notificationType.includes('penalty') || notificationType.includes('Penalty')) return '⚠';
  if (notificationType.includes('noShow') || notificationType.includes('NoShow')) return '⚠';
  if (notificationType.includes('submitted') || notificationType.includes('Submitted')) return '↑';
  if (notificationType.includes('draw') || notificationType.includes('Draw')) return '⟲';
  return 'ℹ';
}

export function NotificationCard({ notification, testID, onPress }: NotificationCardProps) {
  const icon = getNotificationIcon(notification.notificationType);
  const isUnread = !notification.isRead;

  return (
    <Pressable
      testID={testID}
      style={({ pressed }) => [
        styles.card,
        isUnread && styles.cardUnread,
        pressed && styles.cardPressed
      ]}
      onPress={onPress}
    >
      <View style={styles.header}>
        <View style={styles.iconContainer}>
          <Text style={styles.icon}>{icon}</Text>
        </View>
        <View style={styles.headerText}>
          <Text style={[styles.timestamp, isUnread && styles.timestampUnread]}>
            {formatDateTime(notification.createdAt)}
          </Text>
          {isUnread && <View style={styles.unreadDot} />}
        </View>
      </View>

      <Text style={[styles.message, isUnread && styles.messageUnread]}>
        {notification.messageText}
      </Text>

      {(notification.relatedDate || notification.relatedTimeSlot || notification.locationId) && (
        <View style={styles.details}>
          {notification.relatedDate && (
            <Text style={styles.detailText}>📅 {notification.relatedDate}</Text>
          )}
          {notification.relatedTimeSlot && (
            <Text style={styles.detailText}>🕐 {notification.relatedTimeSlot}</Text>
          )}
          {notification.locationId && (
            <Text style={styles.detailText}>📍 {notification.locationId}</Text>
          )}
        </View>
      )}

      {notification.nextAction && notification.nextAction.toLowerCase() !== 'none' && (
        <Text style={styles.nextAction}>
          → {notification.nextAction}
        </Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.cardBackground,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: spacing.sm
  },
  cardUnread: {
    backgroundColor: '#f0f9ff',
    borderColor: colors.primary
  },
  cardPressed: {
    opacity: 0.7
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.sm
  },
  iconContainer: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: colors.backgroundSecondary,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: spacing.sm
  },
  icon: {
    fontSize: 16
  },
  headerText: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between'
  },
  timestamp: {
    fontSize: 12,
    color: colors.textMuted
  },
  timestampUnread: {
    color: colors.primary,
    fontWeight: '600'
  },
  unreadDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: colors.primary
  },
  message: {
    fontSize: 14,
    color: colors.text,
    lineHeight: 20
  },
  messageUnread: {
    fontWeight: '500',
    color: colors.textDark
  },
  details: {
    marginTop: spacing.sm,
    gap: spacing.xs
  },
  detailText: {
    fontSize: 12,
    color: colors.textMuted
  },
  nextAction: {
    marginTop: spacing.sm,
    fontSize: 12,
    color: colors.primary,
    fontWeight: '500'
  }
});
