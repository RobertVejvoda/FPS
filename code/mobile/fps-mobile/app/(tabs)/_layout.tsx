import { Tabs } from 'expo-router';
import { colors } from '@/theme';
import { useUnreadCount } from '@/api/useUnreadCount';
import { useAuth } from '@/auth/AuthContext';
import { isMobileEmployeeRole } from '@/auth/roles';

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
          title: 'Home',
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="bookings"
        options={{
          // UX008 (#781) — module-aware reservations list; short tab label.
          title: 'Reservations',
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="new"
        options={{
          title: 'New',
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="notifications"
        options={{
          title: 'Alerts',
          tabBarBadge: isEmployee && unreadCount > 0 ? unreadCount : undefined,
          href: isEmployee ? undefined : null,
        }}
      />
      <Tabs.Screen
        name="more"
        options={{
          title: 'More',
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
          title: 'Access',
          href: isEmployee ? null : undefined,
        }}
      />
    </Tabs>
  );
}
