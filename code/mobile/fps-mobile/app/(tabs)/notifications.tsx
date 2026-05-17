import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useState } from 'react';
import { StateView } from '@/components/StateView';
import { NotificationCard } from '@/components/NotificationCard';
import { useNotifications } from '@/api/useNotifications';
import { colors, radius, spacing } from '@/theme';

const FILTERS = [
  { key: false as const, label: 'All' },
  { key: true as const, label: 'Unread' },
];

export default function NotificationsRoute() {
  const [unreadOnly, setUnreadOnly] = useState(false);
  const { state, refresh, markRead } = useNotifications(unreadOnly);

  const filterBar = (
    <View style={styles.filterBar}>
      {FILTERS.map(({ key, label }) => (
        <Pressable
          key={String(key)}
          style={[styles.filterTab, unreadOnly === key && styles.filterTabActive]}
          onPress={() => setUnreadOnly(key)}
          accessibilityRole="button"
        >
          <Text style={[styles.filterTabText, unreadOnly === key && styles.filterTabTextActive]}>
            {label}
          </Text>
        </Pressable>
      ))}
    </View>
  );

  function renderContent() {
    if (state.kind === 'idle' || state.kind === 'loading') {
      return <StateView kind="loading" title="Loading notifications…" />;
    }
    if (state.kind === 'unauthenticated') {
      return (
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Sign in to see your parking notifications."
        />
      );
    }
    if (state.kind === 'unreachable') {
      return (
        <StateView
          kind="unreachable"
          title="Backend unreachable"
          message={state.message}
          actionLabel="Retry"
          onAction={refresh}
        />
      );
    }
    if (state.kind === 'error') {
      return (
        <StateView
          kind="error"
          title="Something went wrong"
          message={state.message}
          actionLabel="Retry"
          onAction={refresh}
        />
      );
    }
    if (state.items.length === 0) {
      return (
        <StateView
          kind="empty"
          title={unreadOnly ? 'No unread notifications' : 'No notifications yet'}
          message={
            unreadOnly
              ? 'All caught up. Switch to All to see your history.'
              : 'Booking and allocation updates will appear here.'
          }
          actionLabel="Refresh"
          onAction={refresh}
        />
      );
    }
    return (
      <FlatList
        data={state.items}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <NotificationCard
            notification={item}
            onMarkRead={!item.isRead ? () => markRead(item.id) : undefined}
          />
        )}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl
            refreshing={state.isRefreshing}
            onRefresh={refresh}
            tintColor={colors.primary}
          />
        }
      />
    );
  }

  return (
    <SafeAreaView style={styles.safe}>
      {filterBar}
      {renderContent()}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  filterBar: {
    flexDirection: 'row',
    marginHorizontal: spacing.lg,
    marginTop: spacing.sm,
    marginBottom: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    overflow: 'hidden',
  },
  filterTab: {
    flex: 1,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    backgroundColor: colors.background,
  },
  filterTabActive: { backgroundColor: colors.primary },
  filterTabText: {
    fontSize: 14,
    fontWeight: '500',
    color: colors.textMuted,
  },
  filterTabTextActive: { color: colors.primaryText },
  list: { padding: spacing.lg, gap: spacing.md },
});
