import { Tabs } from 'expo-router';
import { colors } from '@/theme';
import { useUnreadCount } from '@/api/useUnreadCount';
import { useAuth } from '@/auth/AuthContext';
import { isMobileEmployeeRole } from '@/auth/roles';
import { t } from '@/i18n';

export default function TabsLayout() {
  const { roles } = useAuth();
  const isEmployee = isMobileEmployeeRole(roles);
  const unreadCount = useUnreadCount(isEmployee);

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.textMuted,
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: t('nav.home'),
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="bookings"
        options={{
          // UX008 (#781) — module-aware reservations list; short tab label.
          title: t('nav.reservations'),
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="new"
        options={{
          // UX009 (#782) — one date-first Request entry for all enabled modules.
          title: t('nav.request'),
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="notifications"
        options={{
          title: t('nav.alerts'),
          tabBarBadge: isEmployee && unreadCount > 0 ? unreadCount : undefined,
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="more"
        options={{
          title: t('nav.more'),
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          href: null,
        }}
      />
      <Tabs.Screen
        name="unsupported-role"
        options={{
          title: t('nav.access'),
          href: isEmployee ? null : undefined,
        }}
      />
    </Tabs>
  );
}
