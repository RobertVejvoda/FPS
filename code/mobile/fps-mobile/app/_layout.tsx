import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { AuthProvider } from '@/auth/AuthContext';
import { LocaleProvider, t } from '@/i18n';

function RootStack() {
  return (
    <>
      <StatusBar style="auto" />
      <Stack
        screenOptions={{
          headerShown: false,
        }}
      >
        <Stack.Screen name="index" />
        <Stack.Screen name="login" />
        <Stack.Screen name="debug-session" options={{ presentation: 'modal' }} />
        <Stack.Screen name="(tabs)" />
        <Stack.Screen
          name="booking/[requestId]"
          options={{ headerShown: true, title: t('nav.bookingDetail'), headerBackTitle: t('nav.back') }}
        />
      </Stack>
    </>
  );
}

export default function RootLayout() {
  return (
    <AuthProvider>
      {/* LOC001 (#744) — inside AuthProvider so a future tenant-default-locale
          fetch can reuse the session; not built yet (documented follow-up). */}
      <LocaleProvider>
        <RootStack />
      </LocaleProvider>
    </AuthProvider>
  );
}
