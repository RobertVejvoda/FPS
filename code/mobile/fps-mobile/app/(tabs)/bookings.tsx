import { useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StateView } from '@/components/StateView';
import { BookingCard } from '@/components/BookingCard';
import { useBookings } from '@/api/useBookings';
import { colors, radius, spacing } from '@/theme';

const FILTERS = [
  { key: 'upcoming' as const, label: 'Upcoming' },
  { key: 'recent' as const, label: 'Recent' },
];

export default function BookingsRoute() {
  const [filter, setFilter] = useState<'upcoming' | 'recent'>('upcoming');
  const { state, refresh, loadMore } = useBookings(filter);

  const filterBar = (
    <View style={styles.filterBar}>
      {FILTERS.map(({ key, label }) => (
        <Pressable
          key={key}
          style={[styles.filterTab, filter === key && styles.filterTabActive]}
          onPress={() => setFilter(key)}
          accessibilityRole="button"
        >
          <Text style={[styles.filterTabText, filter === key && styles.filterTabTextActive]}>
            {label}
          </Text>
        </Pressable>
      ))}
    </View>
  );

  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <SafeAreaView style={styles.safe}>
        {filterBar}
        <StateView kind="loading" title="Loading bookings…" />
      </SafeAreaView>
    );
  }

  if (state.kind === 'unauthenticated') {
    return (
      <SafeAreaView style={styles.safe}>
        {filterBar}
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Your developer token is missing or rejected. Paste a fresh one to continue."
        />
      </SafeAreaView>
    );
  }

  if (state.kind === 'unreachable') {
    return (
      <SafeAreaView style={styles.safe}>
        {filterBar}
        <StateView
          kind="unreachable"
          title="Backend unreachable"
          message={state.message}
          actionLabel="Retry"
          onAction={refresh}
        />
      </SafeAreaView>
    );
  }

  if (state.kind === 'error') {
    return (
      <SafeAreaView style={styles.safe}>
        {filterBar}
        <StateView
          kind="error"
          title="Something went wrong"
          message={state.message}
          actionLabel="Retry"
          onAction={refresh}
        />
      </SafeAreaView>
    );
  }

  if (state.items.length === 0) {
    return (
      <SafeAreaView style={styles.safe}>
        {filterBar}
        <StateView
          kind="empty"
          title="No bookings yet"
          message="Your parking requests will appear here."
          actionLabel="Refresh"
          onAction={refresh}
        />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe}>
      {filterBar}
      <FlatList
        data={state.items}
        keyExtractor={(item) => item.requestId}
        renderItem={({ item }) => <BookingCard booking={item} />}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl
            refreshing={state.isRefreshing}
            onRefresh={refresh}
            tintColor={colors.primary}
          />
        }
        ListFooterComponent={
          state.nextCursor ? (
            <Pressable
              onPress={loadMore}
              disabled={state.loadingMore}
              accessibilityRole="button"
              style={({ pressed }) => [
                styles.loadMore,
                (pressed || state.loadingMore) && styles.loadMoreDimmed,
              ]}
            >
              {state.loadingMore ? (
                <ActivityIndicator size="small" color={colors.primary} />
              ) : (
                <Text style={styles.loadMoreText}>Load more</Text>
              )}
            </Pressable>
          ) : null
        }
      />
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
  filterTabActive: {
    backgroundColor: colors.primary,
  },
  filterTabText: {
    fontSize: 14,
    fontWeight: '500',
    color: colors.textMuted,
  },
  filterTabTextActive: {
    color: colors.primaryText,
  },
  list: { padding: spacing.lg, gap: spacing.md },
  loadMore: {
    alignItems: 'center',
    paddingVertical: spacing.md,
    marginTop: spacing.sm,
  },
  loadMoreDimmed: { opacity: 0.5 },
  loadMoreText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.primary,
  },
});
