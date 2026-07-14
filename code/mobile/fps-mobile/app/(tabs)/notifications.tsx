import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useState } from 'react';
import { StateView } from '@/components/StateView';
import { NotificationCard } from '@/components/NotificationCard';
import { useNotifications } from '@/api/useNotifications';
import { t } from '@/i18n';
import { colors, radius, spacing } from '@/theme';

export default function NotificationsRoute() {
  const [unreadOnly, setUnreadOnly] = useState(false);
  const { state, refresh, markRead } = useNotifications(unreadOnly);
  const FILTERS = [
    { key: false as const, label: t('notifications.filter.all') },
    { key: true as const, label: t('notifications.filter.unread') },
  ];

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
      return <StateView kind="loading" title={t('notifications.loading')} />;
    }
    if (state.kind === 'unauthenticated') {
      return (
        <StateView
          kind="unauthenticated"
          title={t('session.notSignedIn')}
          message={t('notifications.signInPrompt')}
        />
      );
    }
    if (state.kind === 'unreachable') {
      return (
        <StateView
          kind="unreachable"
          title={t('notifications.cannotLoad')}
          message={t('common.checkConnection')}
          actionLabel={t('common.retry')}
          onAction={refresh}
        />
      );
    }
    if (state.kind === 'error') {
      return (
        <StateView
          kind="error"
          title={t('notifications.cannotLoad')}
          message={t('common.checkConnection')}
          actionLabel={t('common.retry')}
          onAction={refresh}
        />
      );
    }
    if (state.items.length === 0) {
      return (
        <StateView
          kind="empty"
          title={unreadOnly ? t('notifications.empty.unreadTitle') : t('notifications.empty.title')}
          message={
            unreadOnly
              ? t('notifications.empty.unreadMessage')
              : t('notifications.empty.message')
          }
          actionLabel={t('common.refresh')}
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
