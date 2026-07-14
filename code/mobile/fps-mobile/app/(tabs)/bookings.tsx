import { useCallback, useMemo, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, RefreshControl, SectionList, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StateView } from '@/components/StateView';
import { BookingCard } from '@/components/BookingCard';
import { useBookings } from '@/api/useBookings';
import { cancelBooking, confirmBookingUsage } from '@/api/bookings';
import { useAuth } from '@/auth/AuthContext';
import { isSeatsItem } from '@/displayLabels';
import { t } from '@/i18n';
import { formatDate as formatLocaleDate } from '@/i18n/formatters';
import { colors, radius, spacing } from '@/theme';
import type { BookingListItem } from '@/api/bookings';

// UX008 (#781) — date-first grouping for the module-aware reservations list.
// Section order follows the API's cursor order; items keep their fetch order
// inside each date section.
function localTodayStr(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function localTomorrowStr(): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function dateSectionLabel(date: string): string {
  if (date === localTodayStr()) return t('common.today');
  if (date === localTomorrowStr()) return t('common.tomorrow');
  const [y, m, d] = date.split('-').map(Number);
  return formatLocaleDate(new Date(y, m - 1, d), {
    weekday: 'long', month: 'short', day: 'numeric',
  });
}

function toDateSections(items: BookingListItem[]): Array<{ title: string; data: BookingListItem[] }> {
  const sections: Array<{ date: string; title: string; data: BookingListItem[] }> = [];
  for (const item of items) {
    const last = sections[sections.length - 1];
    if (last && last.date === item.requestedDate) {
      last.data.push(item);
    } else {
      sections.push({ date: item.requestedDate, title: dateSectionLabel(item.requestedDate), data: [item] });
    }
  }
  return sections;
}

export default function BookingsRoute() {
  const FILTERS = [
    { key: 'upcoming' as const, label: t('booking.filter.upcoming') },
    { key: 'recent' as const, label: t('booking.filter.recent') },
  ];
  const [filter, setFilter] = useState<'upcoming' | 'recent'>('upcoming');
  const { state, refresh, loadMore } = useBookings(filter);
  const [pendingActionId, setPendingActionId] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);
  const { apiBaseUrl, bearerToken, clearSession } = useAuth();
  const router = useRouter();

  const handleCancel = useCallback((requestId: string) => {
    Alert.alert(
      t('booking.dialog.cancelTitle'),
      t('booking.dialog.cancelMessage'),
      [
        { text: t('booking.dialog.keep'), style: 'cancel' },
        {
          text: t('booking.dialog.cancelTitle'),
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
              setActionMessage({ kind: 'success', text: t('booking.list.cancelled') });
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
        setActionMessage({ kind: 'success', text: t('booking.list.usageAlreadyRecorded') });
      } else {
        setActionMessage({ kind: 'success', text: t('booking.list.usageConfirmed') });
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
      return <StateView kind="loading" title={t('booking.home.loading')} />;
    }
    if (state.kind === 'unauthenticated') {
      return (
        <StateView
          kind="unauthenticated"
          title={t('session.notSignedIn')}
          message={t('session.expiredMessage')}
        />
      );
    }
    if (state.kind === 'unreachable') {
      return (
        <StateView
          kind="unreachable"
          title={t('booking.home.cannotLoad')}
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
          title={t('booking.home.cannotLoad')}
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
          title={t('booking.list.empty.title')}
          message={t('booking.list.empty.message')}
          actionLabel={t('common.refresh')}
          onAction={refresh}
        />
      );
    }
    const showModule = state.items.some(isSeatsItem) && state.items.some((i) => !isSeatsItem(i));
    return (
      <SectionList
        sections={toDateSections(state.items)}
        keyExtractor={(item) => item.requestId}
        renderSectionHeader={({ section }) => (
          <Text style={styles.sectionHeader}>{section.title}</Text>
        )}
        stickySectionHeadersEnabled={false}
        renderItem={({ item }) => (
          <BookingCard
            booking={item}
            showModule={showModule}
            onPress={() => router.push({
              pathname: '/booking/[requestId]',
              params: {
                requestId: item.requestId,
                requestedDate: item.requestedDate,
                timeSlotStart: item.timeSlotStart,
                timeSlotEnd: item.timeSlotEnd,
                locationId: item.locationId ?? '',
                status: item.status,
                reason: item.reason ?? '',
                reasonCode: item.reasonCode ?? '',
                allocatedSlotId: item.allocatedSlotId ?? '',
                createdAt: item.createdAt,
                lastStatusChangedAt: item.lastStatusChangedAt,
                resourceType: item.resourceType ?? 'Parking',
              },
            })}
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
                <Text style={styles.loadMoreText}>{t('booking.list.loadMore')}</Text>
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
  sectionHeader: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.textMuted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginTop: spacing.xs,
  },
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
