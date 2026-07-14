import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Image, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuth } from '@/auth/AuthContext';
import { getOidcConfig, isOidcConfigured } from '@/auth/oidcConfig';
import { loadForcePromptLogin, clearForcePromptLogin } from '@/auth/authStorage';
import { fetchMe } from '@/api/client';
import { t } from '@/i18n';
import { colors, radius, spacing } from '@/theme';

WebBrowser.maybeCompleteAuthSession();

type LoginStatus =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'cancelled' }
  | { kind: 'error'; message: string };

type DiscoveryState = {
  discovery: AuthSession.DiscoveryDocument | null;
  error: string | null;
};

function useOptionalDiscovery(configured: boolean, issuerUrl: string): DiscoveryState {
  const [state, setState] = useState<DiscoveryState>({ discovery: null, error: null });

  useEffect(() => {
    let isMounted = true;
    setState({ discovery: null, error: null });

    if (!configured) return () => { isMounted = false; };

    AuthSession.resolveDiscoveryAsync(issuerUrl)
      .then(result => {
        if (isMounted) setState({ discovery: result, error: null });
      })
      .catch(() => {
        if (isMounted) setState({
          discovery: null,
          error: t('session.identityProviderUnreachable'),
        });
      });

    return () => { isMounted = false; };
  }, [configured, issuerUrl]);

  return state;
}

export default function LoginRoute() {
  const router = useRouter();
  const { setSession, clearSession } = useAuth();
  const oidcConfig = getOidcConfig();
  const configured = isOidcConfigured(oidcConfig);
  const [status, setStatus] = useState<LoginStatus>({ kind: 'idle' });
  // One-time flag set by explicit sign-out; forces interactive OIDC login instead of silent SSO reuse.
  // null means not yet loaded from AsyncStorage; button stays disabled until it resolves.
  const [forcePrompt, setForcePrompt] = useState<boolean | null>(null);

  useEffect(() => {
    loadForcePromptLogin().then(flag => setForcePrompt(flag));
  }, []);

  const redirectUri = AuthSession.makeRedirectUri({ path: 'login-callback' });

  const { discovery, error: discoveryError } = useOptionalDiscovery(configured, oidcConfig.issuerUrl);

  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: oidcConfig.clientId,
      scopes: oidcConfig.scopes.length > 0 ? oidcConfig.scopes : ['openid', 'profile', 'email'],
      redirectUri,
      usePKCE: true,
      // When force-prompt is set (after explicit sign-out), request an interactive login step.
      // This passes prompt=login to the IdP, preventing silent SSO session reuse.
      // Supported by Keycloak, Azure AD, Auth0, and most OIDC-compliant providers.
      // If the provider ignores this parameter, the user may still see silent login;
      // clearing local credentials (done by signOut) remains the reliable part.
      prompt: forcePrompt === true ? AuthSession.Prompt.Login : undefined,
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
      setStatus({ kind: 'error', message: response.error?.message ?? t('session.authorizationFailed') });
      return;
    }
    if (response.type !== 'success') return;

    if (!request?.codeVerifier || !discovery?.tokenEndpoint) {
      setStatus({ kind: 'error', message: t('session.incompleteResponse') });
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
        // Session accepted — clear the force-prompt flag only now so a rejected retry
        // still requires interactive login on the next attempt.
        await clearForcePromptLogin();
        setForcePrompt(false);
        // Enter the shell — unreachable backend is surfaced there
        router.replace('/(tabs)');
        return;
      }
      // 401/403 or server error means the token is not accepted; keep the force-prompt flag
      // so the next sign-in attempt remains interactive.
      await clearSession();
      setStatus({
        kind: 'error',
        message: meResult.kind === 'unauthenticated'
          ? t('session.rejected')
          : t('session.serverError', { status: meResult.status }),
      });
    }).catch(async (err: unknown) => {
      // Token exchange failed; keep the force-prompt flag set for the next attempt.
      await clearSession();
      setStatus({
        kind: 'error',
        message: err instanceof Error ? err.message : t('session.tokenExchangeFailed'),
      });
    });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [response]);

  const isLoading = status.kind === 'loading';
  // forcePrompt === null means the AsyncStorage load is still pending; keep Sign in disabled.
  const canSignIn = configured && !!request && !isLoading && forcePrompt !== null;

  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.container}>
        <Image
          source={require('../assets/icon.png')}
          style={styles.logo}
          accessibilityIgnoresInvertColors
        />
        <Text style={styles.title}>FairSpot</Text>
        <Text style={styles.subtitle}>{t('session.tagline')}</Text>

        {!configured ? (
          <View style={styles.notice}>
            <Text style={styles.noticeText}>{t('session.notConfiguredNotice')}</Text>
          </View>
        ) : null}

        {status.kind === 'cancelled' ? (
          <Text style={styles.hint}>{t('session.cancelled')}</Text>
        ) : status.kind === 'error' ? (
          <Text style={styles.error}>{status.message}</Text>
        ) : discoveryError ? (
          <Text style={styles.error}>{discoveryError}</Text>
        ) : null}

        {configured ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('session.signIn')}
            accessibilityHint={t('session.signInHint')}
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
              <Text style={styles.primaryLabel}>{t('session.signIn')}</Text>
            )}
          </Pressable>
        ) : null}

        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('session.devSessionLabel')}
          accessibilityHint={t('session.devSessionHint')}
          onPress={() => router.push('/debug-session')}
          style={({ pressed }) => [styles.devLink, pressed && styles.devLinkPressed]}
          testID="button-dev-session"
        >
          <Text style={styles.devLinkLabel}>{t('session.devSessionLabel')}</Text>
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
  logo: {
    width: 96,
    height: 96,
    borderRadius: 22,
    marginBottom: spacing.sm,
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
