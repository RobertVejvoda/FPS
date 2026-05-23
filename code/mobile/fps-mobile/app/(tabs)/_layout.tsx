import { Tabs } from 'expo-router';
import { colors } from '@/theme';
import { useUnreadCount } from '@/api/useUnreadCount';
import { useAuth } from '@/auth/AuthContext';
import { isMobileEmployeeRole } from '@/auth/roles';

export default function TabsLayout() {
  const unreadCount = useUnreadCount();
  const { roles } = useAuth();
  const isEmployee = isMobileEmployeeRole(roles);

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
          title: 'My Bookings',
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
        name="profile"
        options={{
          title: 'Profile',
          href: isEmployee ? undefined : null,
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
