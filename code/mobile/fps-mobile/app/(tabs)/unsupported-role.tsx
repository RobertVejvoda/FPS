import { StyleSheet, Text, View } from 'react-native';
import { colors } from '@/theme';
import { useAuth } from '@/auth/AuthContext';
import { t } from '@/i18n';

export default function UnsupportedRoleScreen() {
  const { roles } = useAuth();
  const roleList = roles.length > 0 ? roles.join(', ') : 'unknown';

  return (
    <View style={styles.container}>
      <Text style={styles.title}>{t('session.unsupportedRole.title')}</Text>
      <Text style={styles.body}>
        {t('session.unsupportedRole.body')}
      </Text>
      <Text style={styles.detail}>{t('session.unsupportedRole.roleLine', { roleList })}</Text>
      <Text style={styles.hint}>
        {t('session.unsupportedRole.hint')}
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
