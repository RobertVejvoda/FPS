import { Tabs } from 'expo-router';
import { colors } from '@/theme';
import { useUnreadCount } from '@/api/useUnreadCount';

export default function TabsLayout() {
  const unreadCount = useUnreadCount();

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.textMuted,
      }}
    >
      <Tabs.Screen name="index" options={{ title: 'Home' }} />
      <Tabs.Screen name="bookings" options={{ title: 'My Bookings' }} />
      <Tabs.Screen name="new" options={{ title: 'New' }} />
      <Tabs.Screen
        name="notifications"
        options={{
          title: 'Alerts',
          tabBarBadge: unreadCount > 0 ? unreadCount : undefined,
        }}
      />
      <Tabs.Screen name="profile" options={{ title: 'Profile' }} />
    </Tabs>
  );
}
