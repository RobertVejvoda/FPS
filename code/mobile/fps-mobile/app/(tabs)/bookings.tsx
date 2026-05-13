import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';
import { BookingCard } from '@/components/BookingCard';
import { useBookings } from '@/api/useBookings';
import { colors, spacing } from '@/theme';

export default function BookingsRoute() {
  const { state, refresh, loadMore } = useBookings();

  if (state.kind === 'idle' || state.kind === 'loading') {
    return (
      <Screen>
        <StateView kind="loading" title="Loading bookings…" />
      </Screen>
    );
  }

  if (state.kind === 'unauthenticated') {
    return (
      <Screen>
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Your developer token is missing or rejected. Paste a fresh one to continue."
        />
      </Screen>
    );
  }

  if (state.kind === 'unreachable') {
    return (
      <Screen>
        <StateView
          kind="unreachable"
          title="Backend unreachable"
          message={state.message}
          actionLabel="Retry"
          onAction={refresh}
        />
      </Screen>
    );
  }

  if (state.kind === 'error') {
    return (
      <Screen>
        <StateView
          kind="error"
          title="Something went wrong"
          message={state.message}
          actionLabel="Retry"
          onAction={refresh}
        />
      </Screen>
    );
  }

  if (state.items.length === 0) {
    return (
      <Screen>
        <StateView
          kind="empty"
          title="No bookings yet"
          message="Your parking requests will appear here."
          actionLabel="Refresh"
          onAction={refresh}
        />
      </Screen>
    );
  }

  return (
    <SafeAreaView style={styles.safe}>
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
