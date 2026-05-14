import { useCallback, useMemo, useState } from 'react';
import { ActivityIndicator, Alert, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StateView } from '@/components/StateView';
import { BookingCard } from '@/components/BookingCard';
import { useBookings } from '@/api/useBookings';
import { cancelBooking, confirmBookingUsage } from '@/api/bookings';
import { useAuth } from '@/auth/AuthContext';
import { colors, radius, spacing } from '@/theme';

const FILTERS = [
  { key: 'upcoming' as const, label: 'Upcoming' },
  { key: 'recent' as const, label: 'Recent' },
];

export default function BookingsRoute() {
  const [filter, setFilter] = useState<'upcoming' | 'recent'>('upcoming');
  const { state, refresh, loadMore } = useBookings(filter);
  const [pendingActionId, setPendingActionId] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);
  const { apiBaseUrl, bearerToken, clearSession } = useAuth();
  const router = useRouter();

  const handleCancel = useCallback((requestId: string) => {
    Alert.alert(
      'Cancel booking',
      'Are you sure you want to cancel this parking request?',
      [
        { text: 'Keep', style: 'cancel' },
        {
          text: 'Cancel booking',
          style: 'destructive',
          onPress: async () => {
            setActionMessage(null);
            setPendingActionId(requestId);
            const result = await cancelBooking({ apiBaseUrl, bearerToken }, requestId, 'Cancelled from mobile app');
            setPendingActionId(null);
            if (result.kind === 'unauthenticated') {
              await clearSession();
              router.replace('/login');
            } else if (result.kind === 'ok') {
              setActionMessage({ kind: 'success', text: 'Booking cancelled.' });
              refresh();
            } else if (result.kind === 'notFound') {
              setActionMessage({ kind: 'error', text: result.message });
              refresh();
            } else if (result.kind === 'unreachable') {
              setActionMessage({ kind: 'error', text: result.message });
            } else {
              setActionMessage({ kind: 'error', text: result.message });
            }
          },
        },
      ],
    );
  }, [apiBaseUrl, bearerToken, clearSession, router, refresh]);

  const handleConfirmUsage = useCallback(async (requestId: string) => {
    setActionMessage(null);
    setPendingActionId(requestId);
    const result = await confirmBookingUsage({ apiBaseUrl, bearerToken }, requestId);
    setPendingActionId(null);
    if (result.kind === 'unauthenticated') {
      await clearSession();
      router.replace('/login');
    } else if (result.kind === 'confirmed') {
      if (result.wasAlreadyConfirmed) {
        setActionMessage({ kind: 'success', text: 'Your parking usage was already recorded.' });
      } else {
        setActionMessage({ kind: 'success', text: 'Parking usage confirmed.' });
      }
      refresh();
    } else if (result.kind === 'notFound') {
      setActionMessage({ kind: 'error', text: result.message });
      refresh();
    } else if (result.kind === 'unreachable') {
      setActionMessage({ kind: 'error', text: result.message });
    } else {
      setActionMessage({ kind: 'error', text: result.message });
    }
  }, [apiBaseUrl, bearerToken, clearSession, router, refresh]);

  const filterBar = useMemo(() => (
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
  ), [filter]);

  function renderContent() {
    if (state.kind === 'idle' || state.kind === 'loading') {
      return <StateView kind="loading" title="Loading bookings…" />;
    }
    if (state.kind === 'unauthenticated') {
      return (
        <StateView
          kind="unauthenticated"
          title="Not signed in"
          message="Your developer token is missing or rejected. Paste a fresh one to continue."
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
          title="No bookings yet"
          message="Your parking requests will appear here."
          actionLabel="Refresh"
          onAction={refresh}
        />
      );
    }
    return (
      <FlatList
        data={state.items}
        keyExtractor={(item) => item.requestId}
        renderItem={({ item }) => (
          <BookingCard
            booking={item}
            onCancel={item.nextAction === 'cancel' ? () => handleCancel(item.requestId) : undefined}
            onConfirmUsage={item.nextAction === 'confirmUsage' ? () => handleConfirmUsage(item.requestId) : undefined}
            actionPending={pendingActionId === item.requestId}
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
    );
  }

  return (
    <SafeAreaView style={styles.safe}>
      {filterBar}
      {actionMessage ? (
        <View
          style={[
            styles.actionMessage,
            actionMessage.kind === 'error' ? styles.actionError : styles.actionSuccess,
          ]}
        >
          <Text
            style={[
              styles.actionMessageText,
              actionMessage.kind === 'error' ? styles.actionErrorText : styles.actionSuccessText,
            ]}
          >
            {actionMessage.text}
          </Text>
        </View>
      ) : null}
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
  actionMessage: {
    marginHorizontal: spacing.lg,
    marginBottom: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    padding: spacing.md,
  },
  actionSuccess: {
    backgroundColor: '#ecfdf5',
    borderColor: '#bbf7d0',
  },
  actionError: {
    backgroundColor: '#fef2f2',
    borderColor: '#fecaca',
  },
  actionMessageText: {
    fontSize: 13,
    fontWeight: '500',
  },
  actionSuccessText: {
    color: '#166534',
  },
  actionErrorText: {
    color: colors.danger,
  },
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
