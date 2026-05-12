import { StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '@/theme';

type PlaceholderCardProps = {
  title: string;
  description: string;
  testID?: string;
};

// Plain visual stub shared by tab screens whose feature work lives in later slices.
export function PlaceholderCard({ title, description, testID }: PlaceholderCardProps) {
  return (
    <View style={styles.card} testID={testID}>
      <Text style={styles.title}>{title}</Text>
      <Text style={styles.description}>{description}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    padding: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.cardBackground,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.xs,
  },
  title: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text,
  },
  description: {
    fontSize: 13,
    color: colors.textMuted,
  },
});
