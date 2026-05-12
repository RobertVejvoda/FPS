import { Redirect } from 'expo-router';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { colors } from '@/theme';

// Auth gate: until dev credentials are stored we route to the debug-session screen.
// Real login is intentionally out of scope for MOB001.
export default function IndexRoute() {
  const { ready, isConfigured } = useAuth();

  if (!ready) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return isConfigured ? <Redirect href="/(tabs)" /> : <Redirect href="/debug-session" />;
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
});
