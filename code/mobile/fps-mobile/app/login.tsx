import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuth } from '@/auth/AuthContext';
import { getOidcConfig, isOidcConfigured } from '@/auth/oidcConfig';
import { clearAccessToken } from '@/auth/authStorage';
import { fetchMe } from '@/api/client';
import { colors, radius, spacing } from '@/theme';

WebBrowser.maybeCompleteAuthSession();

type LoginStatus =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'cancelled' }
  | { kind: 'error'; message: string };

export default function LoginRoute() {
  const router = useRouter();
  const { setSession } = useAuth();
  const oidcConfig = getOidcConfig();
  const configured = isOidcConfigured(oidcConfig);
  const [status, setStatus] = useState<LoginStatus>({ kind: 'idle' });

  const redirectUri = AuthSession.makeRedirectUri({ path: 'login-callback' });

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const discovery = AuthSession.useAutoDiscovery((configured ? oidcConfig.issuerUrl : null) as any);

  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: oidcConfig.clientId,
      scopes: oidcConfig.scopes.length > 0 ? oidcConfig.scopes : ['openid', 'profile', 'email'],
      redirectUri,
      usePKCE: true,
    },
    discovery,
  );

  useEffect(() => {
    if (!response) return;

    if (response.type === 'cancel' || response.type === 'dismiss') {
      setStatus({ kind: 'cancelled' });
      return;
    }
    if (response.type === 'error') {
      setStatus({ kind: 'error', message: response.error?.message ?? 'Authorization failed.' });
      return;
    }
    if (response.type !== 'success') return;

    if (!request?.codeVerifier || !discovery?.tokenEndpoint) {
      setStatus({ kind: 'error', message: 'Incomplete OIDC response.' });
      return;
    }

    setStatus({ kind: 'loading' });

    AuthSession.exchangeCodeAsync(
      {
        clientId: oidcConfig.clientId,
        code: response.params.code,
        redirectUri,
        extraParams: { code_verifier: request.codeVerifier ?? '' },
      },
      { tokenEndpoint: discovery.tokenEndpoint },
    ).then(async (tokenResponse) => {
      const { accessToken } = tokenResponse;
      await setSession(accessToken);

      const meResult = await fetchMe({ apiBaseUrl: oidcConfig.apiBaseUrl, bearerToken: accessToken });

      if (meResult.kind === 'ok' || meResult.kind === 'unreachable') {
        // Enter the shell — unreachable backend is surfaced there
        router.replace('/(tabs)');
        return;
      }
      // 401/403 or server error means the token is not accepted
      await clearAccessToken();
      setStatus({
        kind: 'error',
        message: meResult.kind === 'unauthenticated'
          ? 'Session was rejected. Please sign in again.'
          : `Server error (${meResult.status}). Please try again.`,
      });
    }).catch((err: unknown) => {
      setStatus({
        kind: 'error',
        message: err instanceof Error ? err.message : 'Token exchange failed.',
      });
    });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [response]);

  const isLoading = status.kind === 'loading';
  const canSignIn = configured && !!request && !isLoading;

  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.container}>
        <Text style={styles.title}>FPS Mobile</Text>
        <Text style={styles.subtitle}>Parking management for employees</Text>

        {!configured ? (
          <View style={styles.notice}>
            <Text style={styles.noticeText}>
              Login is not configured for this build.{'\n'}Use the developer session option below.
            </Text>
          </View>
        ) : null}

        {status.kind === 'cancelled' ? (
          <Text style={styles.hint}>Sign in was cancelled.</Text>
        ) : status.kind === 'error' ? (
          <Text style={styles.error}>{status.message}</Text>
        ) : null}

        {configured ? (
          <Pressable
            accessibilityRole="button"
            disabled={!canSignIn}
            onPress={() => {
              setStatus({ kind: 'loading' });
              promptAsync();
            }}
            style={({ pressed }) => [
              styles.primary,
              (!canSignIn || pressed) && styles.primaryDimmed,
            ]}
            testID="button-sign-in"
          >
            {isLoading ? (
              <ActivityIndicator color={colors.primaryText} />
            ) : (
              <Text style={styles.primaryLabel}>Sign in</Text>
            )}
          </Pressable>
        ) : null}

        <Pressable
          accessibilityRole="button"
          onPress={() => router.push('/debug-session')}
          style={({ pressed }) => [styles.devLink, pressed && styles.devLinkPressed]}
          testID="button-dev-session"
        >
          <Text style={styles.devLinkLabel}>Developer session</Text>
        </Pressable>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: spacing.xl,
    gap: spacing.md,
  },
  title: { fontSize: 28, fontWeight: '700', color: colors.text },
  subtitle: { fontSize: 15, color: colors.textMuted, textAlign: 'center' },
  notice: {
    backgroundColor: colors.cardBackground,
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
  },
  noticeText: { fontSize: 13, color: colors.textMuted, textAlign: 'center', lineHeight: 20 },
  hint: { fontSize: 13, color: colors.textMuted, textAlign: 'center' },
  error: { fontSize: 13, color: colors.danger, textAlign: 'center' },
  primary: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.xl,
    alignItems: 'center',
    width: '100%',
    minHeight: 48,
    justifyContent: 'center',
  },
  primaryDimmed: { opacity: 0.5 },
  primaryLabel: { color: colors.primaryText, fontWeight: '700', fontSize: 16 },
  devLink: { marginTop: spacing.sm, padding: spacing.sm },
  devLinkPressed: { opacity: 0.6 },
  devLinkLabel: { color: colors.textMuted, fontSize: 13, textDecorationLine: 'underline' },
});
