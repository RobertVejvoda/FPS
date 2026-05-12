import { useRouter } from 'expo-router';
import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { Screen } from '@/components/Screen';
import { colors, radius, spacing } from '@/theme';

// Dev-only credential handoff. The bearer token never leaves AsyncStorage on the
// device, and nothing about this screen ships in a production build path —
// MOB001's purpose is to scaffold the shell, not implement real login.
export default function DebugTokenRoute() {
  const router = useRouter();
  const { apiBaseUrl, bearerToken, saveCredentials, clearCredentials } = useAuth();
  const [baseUrlInput, setBaseUrlInput] = useState(apiBaseUrl);
  const [tokenInput, setTokenInput] = useState(bearerToken);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSave = baseUrlInput.trim().length > 0 && tokenInput.trim().length > 0;

  return (
    <Screen testID="debug-session-screen">
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.form}
      >
        <Text style={styles.heading}>Developer Session</Text>
        <Text style={styles.lead}>
          Paste an API base URL and bearer token from a development backend run. These values
          stay on this device and are not committed anywhere. Real login is implemented in a
          later slice.
        </Text>

        <View style={styles.field}>
          <Text style={styles.label}>API base URL</Text>
          <TextInput
            value={baseUrlInput}
            onChangeText={setBaseUrlInput}
            autoCapitalize="none"
            autoCorrect={false}
            keyboardType="url"
            placeholder="http://localhost:5100"
            placeholderTextColor={colors.textMuted}
            style={styles.input}
            testID="input-api-base-url"
          />
        </View>

        <View style={styles.field}>
          <Text style={styles.label}>Bearer token</Text>
          <TextInput
            value={tokenInput}
            onChangeText={setTokenInput}
            autoCapitalize="none"
            autoCorrect={false}
            multiline
            placeholder="eyJhbGciOiJI..."
            placeholderTextColor={colors.textMuted}
            style={[styles.input, styles.inputMultiline]}
            testID="input-bearer-token"
          />
        </View>

        {error ? <Text style={styles.error}>{error}</Text> : null}

        <Pressable
          accessibilityRole="button"
          disabled={!canSave || saving}
          onPress={async () => {
            try {
              setSaving(true);
              setError(null);
              await saveCredentials(baseUrlInput, tokenInput);
              router.replace('/(tabs)');
            } catch (cause) {
              setError(cause instanceof Error ? cause.message : 'Could not save credentials.');
            } finally {
              setSaving(false);
            }
          }}
          style={({ pressed }) => [
            styles.primary,
            (!canSave || saving) && styles.primaryDisabled,
            pressed && styles.primaryPressed,
          ]}
          testID="button-save-credentials"
        >
          <Text style={styles.primaryLabel}>{saving ? 'Saving…' : 'Save and continue'}</Text>
        </Pressable>

        {bearerToken.length > 0 ? (
          <Pressable
            accessibilityRole="button"
            onPress={async () => {
              await clearCredentials();
              setBaseUrlInput('');
              setTokenInput('');
            }}
            style={({ pressed }) => [styles.secondary, pressed && styles.secondaryPressed]}
            testID="button-clear-credentials"
          >
            <Text style={styles.secondaryLabel}>Clear stored credentials</Text>
          </Pressable>
        ) : null}
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  form: { gap: spacing.md },
  heading: { fontSize: 20, fontWeight: '700', color: colors.text },
  lead: { fontSize: 14, color: colors.textMuted, lineHeight: 20 },
  field: { gap: spacing.xs },
  label: { fontSize: 13, fontWeight: '600', color: colors.text },
  input: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: 14,
    color: colors.text,
    backgroundColor: colors.background,
  },
  inputMultiline: { minHeight: 80, textAlignVertical: 'top' },
  primary: {
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.md,
    backgroundColor: colors.primary,
    alignItems: 'center',
  },
  primaryDisabled: { opacity: 0.4 },
  primaryPressed: { opacity: 0.8 },
  primaryLabel: { color: colors.primaryText, fontWeight: '700' },
  secondary: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    alignItems: 'center',
  },
  secondaryPressed: { opacity: 0.6 },
  secondaryLabel: { color: colors.danger, fontWeight: '600' },
  error: { color: colors.danger, fontSize: 13 },
});
