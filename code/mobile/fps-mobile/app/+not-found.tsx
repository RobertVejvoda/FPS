import { Link } from 'expo-router';
import { Screen } from '@/components/Screen';
import { StateView } from '@/components/StateView';
import { StyleSheet, Text } from 'react-native';
import { colors, spacing } from '@/theme';

export default function NotFoundRoute() {
  return (
    <Screen>
      <StateView
        kind="error"
        title="Page not found"
        message="The route you tried to open does not exist in this app."
      />
      <Link href="/" style={styles.link}>
        <Text style={styles.linkText}>Go to home</Text>
      </Link>
    </Screen>
  );
}

const styles = StyleSheet.create({
  link: { alignSelf: 'center', padding: spacing.md },
  linkText: { color: colors.primary, fontWeight: '600' },
});
