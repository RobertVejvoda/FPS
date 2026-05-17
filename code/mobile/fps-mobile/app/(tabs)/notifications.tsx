import { FlatList, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';
import { NotificationCard } from '@/components/NotificationCard';
import { useNotifications } from '@/api/useNotifications';
import type { NotificationDto } from '@/api/notifications';
import { colors, spacing } from '@/theme';
import { useState } from 'react';

export default function NotificationsRoute() {
  const { state, refresh, markRead } = useNotifications();
  const [actionMessage, setActionMessage] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);

  const handleNotificationPress = async (notification: NotificationDto) => {
    if (notification.isRead) return;

    const result = await markRead(notification.id);

    if (result.kind === 'ok') {
      // Silently mark as read
    } else if (result.kind === 'notFound') {
      setActionMessage({ kind: 'error', text: 'Notification not found' });
      setTimeout(() => setActionMessage(null), 3000);
    } else {
      setActionMessage({ kind: 'error', text: 'Failed to mark notification as read' });
      setTimeout(() => setActionMessage(null), 3000);
    }
  };

  const renderNotification = ({ item }: { item: NotificationDto }) => (
    <NotificationCard
      notification={item}
      testID={`notification-${item.id}`}
      onPress={() => handleNotificationPress(item)}
    />
  );

  const keyExtractor = (item: NotificationDto) => item.id;

  if (state.kind === 'loading') {
    return (
      <Screen>
        <StateView kind="loading" title="Loading notifications..." />
      </Screen>
    );
  }

  if (state.kind === 'unauthenticated') {
    return (
      <Screen>
        <StateView kind="unauthenticated" title="Not authenticated" message="Please sign in to view notifications." />
      </Screen>
    );
  }

  if (state.kind === 'unreachable') {
    return (
      <Screen>
        <StateView kind="unreachable" title="Connection failed" message={state.message} />
      </Screen>
    );
  }

  if (state.kind === 'error') {
    return (
      <Screen>
        <StateView kind="error" title={`HTTP ${state.status}`} message={state.message} />
      </Screen>
    );
  }

  if (state.kind === 'ok' && state.items.length === 0) {
    return (
      <Screen>
        <StateView kind="empty" title="No notifications" message="You'll see booking updates here." />
      </Screen>
    );
  }

  return (
    <Screen>
      <View style={styles.container}>
        {actionMessage && (
          <View
            style={[
              styles.actionMessage,
              actionMessage.kind === 'success' ? styles.actionMessageSuccess : styles.actionMessageError
            ]}
          >
            <Text style={styles.actionMessageText}>{actionMessage.text}</Text>
          </View>
        )}

        <View style={styles.header}>
          <Text style={styles.title}>Notifications</Text>
          {state.kind === 'ok' && state.unreadCount > 0 && (
            <View style={styles.badge}>
              <Text style={styles.badgeText}>{state.unreadCount} unread</Text>
            </View>
          )}
        </View>

        <FlatList
          data={state.kind === 'ok' ? state.items : []}
          renderItem={renderNotification}
          keyExtractor={keyExtractor}
          contentContainerStyle={styles.list}
          refreshControl={
            <RefreshControl
              refreshing={state.kind === 'ok' && state.isRefreshing}
              onRefresh={refresh}
              tintColor={colors.primary}
            />
          }
          ListEmptyComponent={
            <StateView kind="empty" title="No notifications" message="You'll see booking updates here." />
          }
        />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.background
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    paddingTop: spacing.md,
    paddingBottom: spacing.sm
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: colors.text
  },
  badge: {
    backgroundColor: colors.primary,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: 12
  },
  badgeText: {
    color: colors.primaryText,
    fontSize: 12,
    fontWeight: '600'
  },
  list: {
    padding: spacing.md
  },
  actionMessage: {
    margin: spacing.md,
    padding: spacing.md,
    borderRadius: 8
  },
  actionMessageSuccess: {
    backgroundColor: '#d1fae5',
    borderWidth: 1,
    borderColor: '#10b981'
  },
  actionMessageError: {
    backgroundColor: '#fee2e2',
    borderWidth: 1,
    borderColor: '#ef4444'
  },
  actionMessageText: {
    fontSize: 14,
    fontWeight: '500'
  }
});

