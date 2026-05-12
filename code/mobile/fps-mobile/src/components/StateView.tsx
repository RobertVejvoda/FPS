import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '@/theme';

type Kind = 'loading' | 'empty' | 'error' | 'unauthenticated' | 'unreachable';

type StateViewProps = {
  kind: Kind;
  title: string;
  message?: string;
  actionLabel?: string;
  onAction?: () => void;
  testID?: string;
};

// Shell-level placeholder for the five required states. Each screen composes this
// component until real feature slices replace it.
export function StateView({ kind, title, message, actionLabel, onAction, testID }: StateViewProps) {
  return (
    <View style={styles.container} testID={testID ?? `state-${kind}`}>
      {kind === 'loading' && <ActivityIndicator size="large" color={colors.primary} />}
      <Text style={[styles.title, kind === 'error' && styles.titleError]}>{title}</Text>
      {message ? <Text style={styles.message}>{message}</Text> : null}
      {actionLabel && onAction ? (
        <Pressable
          accessibilityRole="button"
          onPress={onAction}
          style={({ pressed }) => [styles.action, pressed && styles.actionPressed]}
        >
          <Text style={styles.actionLabel}>{actionLabel}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: spacing.md,
    paddingVertical: spacing.xl,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text,
    textAlign: 'center',
  },
  titleError: {
    color: colors.danger,
  },
  message: {
    fontSize: 14,
    color: colors.textMuted,
    textAlign: 'center',
  },
  action: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.md,
    backgroundColor: colors.primary,
  },
  actionPressed: { opacity: 0.7 },
  actionLabel: {
    color: colors.primaryText,
    fontWeight: '600',
  },
});
