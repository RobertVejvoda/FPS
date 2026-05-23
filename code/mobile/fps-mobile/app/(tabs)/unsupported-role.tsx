import { StyleSheet, Text, View } from 'react-native';
import { colors } from '@/theme';
import { useAuth } from '@/auth/AuthContext';

export default function UnsupportedRoleScreen() {
  const { roles } = useAuth();
  const roleList = roles.length > 0 ? roles.join(', ') : 'unknown';

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Mobile Access Not Available</Text>
      <Text style={styles.body}>
        The FairSpot mobile app is currently available for employees only.
      </Text>
      <Text style={styles.detail}>Your role: {roleList}</Text>
      <Text style={styles.hint}>
        Use the web app to access admin, reporting, or audit features.
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 32,
    backgroundColor: colors.background,
    gap: 12,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text,
    textAlign: 'center',
  },
  body: {
    fontSize: 15,
    color: colors.text,
    textAlign: 'center',
  },
  detail: {
    fontSize: 13,
    color: colors.textMuted,
    textAlign: 'center',
  },
  hint: {
    fontSize: 13,
    color: colors.textMuted,
    textAlign: 'center',
    marginTop: 8,
  },
});
