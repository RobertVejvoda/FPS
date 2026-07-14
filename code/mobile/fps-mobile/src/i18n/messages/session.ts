// LOC001 (#744) — sign-in / session / unsupported-role copy
// (app/login.tsx, app/(tabs)/unsupported-role.tsx, and session-expiry states
// reused from other screens). Mirrors code/web/fps-web/src/i18n/messages/session.ts.
import type { Catalog } from '../catalog';

const en = {
  'session.signIn': 'Sign in',
  'session.signInHint': 'Opens the identity provider login page',
  'session.signOut': 'Sign out',
  'session.notSignedIn': 'Not signed in',
  'session.expiredMessage': 'Your session has expired. Please sign in again.',
  'session.cancelled': 'Sign in was cancelled.',
  'session.identityProviderUnreachable': 'Identity provider is not reachable from this device.',
  'session.authorizationFailed': 'Authorization failed.',
  'session.incompleteResponse': 'Incomplete OIDC response.',
  'session.rejected': 'Session was rejected. Please sign in again.',
  'session.serverError': 'Server error ({status}). Please try again.',
  'session.tokenExchangeFailed': 'Token exchange failed.',
  'session.tagline': 'Fair access to workplace parking',
  'session.notConfiguredNotice': 'Login is not configured for this build.\nUse the developer session option below.',
  'session.devSessionLabel': 'Developer session',
  'session.devSessionHint': 'Enter an API base URL and bearer token manually',

  // Unsupported-role screen (app/(tabs)/unsupported-role.tsx)
  'session.unsupportedRole.title': 'Mobile Access Not Available',
  'session.unsupportedRole.body': 'The FairSpot mobile app is currently available for employees only.',
  'session.unsupportedRole.roleLine': 'Your role: {roleList}',
  'session.unsupportedRole.hint': 'Use the web app to access admin, reporting, or audit features.',
} as const;

const cs: Catalog<keyof typeof en> = {
  'session.signIn': 'Přihlásit se',
  'session.signInHint': 'Otevře přihlašovací stránku poskytovatele identity',
  'session.signOut': 'Odhlásit se',
  'session.notSignedIn': 'Nejste přihlášeni',
  'session.expiredMessage': 'Vaše relace vypršela. Přihlaste se prosím znovu.',
  'session.cancelled': 'Přihlášení bylo zrušeno.',
  'session.identityProviderUnreachable': 'Poskytovatel identity není z tohoto zařízení dostupný.',
  'session.authorizationFailed': 'Autorizace se nezdařila.',
  'session.incompleteResponse': 'Neúplná odpověď OIDC.',
  'session.rejected': 'Relace byla odmítnuta. Přihlaste se prosím znovu.',
  'session.serverError': 'Chyba serveru ({status}). Zkuste to prosím znovu.',
  'session.tokenExchangeFailed': 'Výměna tokenu se nezdařila.',
  'session.tagline': 'Spravedlivý přístup k parkování v práci',
  'session.notConfiguredNotice': 'Přihlášení není pro tuto verzi aplikace nastaveno.\nPoužijte níže vývojářskou relaci.',
  'session.devSessionLabel': 'Vývojářská relace',
  'session.devSessionHint': 'Zadejte základní URL adresu API a nositelský token ručně',

  'session.unsupportedRole.title': 'Mobilní přístup není k dispozici',
  'session.unsupportedRole.body': 'Mobilní aplikace FairSpot je momentálně dostupná pouze pro zaměstnance.',
  'session.unsupportedRole.roleLine': 'Vaše role: {roleList}',
  'session.unsupportedRole.hint': 'Pro přístup ke správě, přehledům nebo auditu použijte webovou aplikaci.',
};

export const sessionMessages = { en, cs };
