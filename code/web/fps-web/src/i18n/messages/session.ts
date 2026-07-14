// LOC001 (#744) — sign-in / session / auth callback / email verification copy.
import type { Catalog } from '../catalog';

const en = {
  'session.phase.loginCancelled': 'Sign in was cancelled. Try again.',
  'session.phase.loginFailed': 'Sign in failed. Try again.',
  'session.phase.sessionExpired': 'Your session has expired. Please sign in again.',
  'session.phase.unreachable': 'Cannot reach the backend. Check your connection and try again.',

  'session.eyebrow.workplace': 'Workplace parking operations',
  'session.hero.title1': 'Fair allocation with evidence your business can trust.',
  'session.hero.body1':
    'Request shared spots and seats, run policy-based Draws, and give HR a clear operational record without exposing private employee data.',
  'session.verifying': 'Verifying session…',

  'session.eyebrow.config': 'Runtime configuration',
  'session.config.title': 'Configuration needs attention.',
  'session.config.body': 'The app cannot load the runtime identity settings required for sign-in.',
  'session.config.errorHeading': 'Configuration error',
  'session.config.errorFallback': 'Unable to load /config.json.',
  'session.config.hintPrefix': 'Ensure',
  'session.config.hintSuffix': 'is served by the web server and contains valid OIDC settings.',

  'session.eyebrow.brand': 'FairSpot for modern workplaces',
  'session.hero.title2': 'Fair allocation employees can understand.',
  'session.hero.body2':
    'Give employees a clear answer, give HR operational control, and keep every Draw ready for review.',
  'session.legalLink': 'Legal notices',

  'session.signIn.heading': 'Sign in',
  'session.signIn.secureAccessFor': 'Secure access for {tenantName}.',
  'session.signIn.secureAccessDefault': 'Secure access to employee and HR workspaces.',

  'session.security.fairAllocation': 'Fair allocation',
  'session.security.teamPolicies': 'Team policies',
  'session.security.clearOutcomes': 'Clear outcomes',

  'session.dev.hide': 'Hide development access',
  'session.dev.show': 'Development access',
  'session.dev.note': 'Development only. Paste the token from the local smoke script.',
  'session.dev.apiBaseUrl': 'API base URL',
  'session.dev.bearerToken': 'Bearer token',
  'session.dev.verifying': 'Verifying…',
  'session.dev.useToken': 'Use token',
  'session.dev.clearToken': 'Clear stored token',
  'session.dev.bothRequired': 'Both fields are required.',

  'session.snapshot.ariaLabel': 'Operational highlights',
  'session.snapshot.nextDraw': 'Next Draw',
  'session.snapshot.nextDrawSub': 'Policy window visible to employees',
  'session.snapshot.hrView': 'HR view',
  'session.snapshot.hrViewValue': 'Live',
  'session.snapshot.hrViewSub': 'Requests, outcomes, and exceptions',
  'session.snapshot.evidence': 'Evidence',
  'session.snapshot.evidenceValue': 'Traceable',
  'session.snapshot.evidenceSub': 'Allocation decisions kept for review',

  'session.email.label': 'Email',
  'session.email.notFound':
    "We couldn't find a sign-in route for that email. Check the address and try again, or sign in with a FairSpot account. If you keep getting stuck, contact your company's FairSpot administrator.",
  'session.email.error': 'Something went wrong while finding your sign-in. Try again, or sign in with a FairSpot account.',
  'session.email.finding': 'Finding your sign-in…',
  'session.email.routingSso': 'Taking you to your company sign-in…',
  'session.email.routingLocal': 'Taking you to FairSpot sign-in…',
  'session.email.continue': 'Continue',
  'session.email.signInFairspot': 'Sign in with a FairSpot account instead',

  'session.callback.completing': 'Completing sign in…',

  'session.verify.brandSubtitle': 'Email verification',
  'session.verify.heading': 'Confirm your email address',
  'session.verify.confirming': 'Confirming your email address…',
  'session.verify.verified': 'Your email address is confirmed. You can now receive FairSpot notifications.',
  'session.verify.needSignIn':
    'Please sign in to FairSpot with this account, then open the verification link from your email again to confirm. Confirmation is tied to your own signed-in session.',
  'session.verify.rejected': 'This verification link is no longer valid. Request a new verification email from your FairSpot profile and try again.',
  'session.verify.rejectedExpired':
    'This verification link is no longer valid — it has expired. Request a new verification email from your FairSpot profile and try again.',
  'session.verify.noToken': 'This link is missing its verification code. Open the link directly from your FairSpot email.',
  'session.verify.error': "We couldn't confirm your email right now. Please try the link again shortly.",
  'session.verify.goToFairspot': 'Go to FairSpot',
} as const;

const cs: Catalog<keyof typeof en> = {
  'session.phase.loginCancelled': 'Přihlášení bylo zrušeno. Zkuste to prosím znovu.',
  'session.phase.loginFailed': 'Přihlášení se nezdařilo. Zkuste to prosím znovu.',
  'session.phase.sessionExpired': 'Vaše relace vypršela. Přihlaste se prosím znovu.',
  'session.phase.unreachable': 'Nepodařilo se spojit se serverem. Zkontrolujte připojení a zkuste to prosím znovu.',

  'session.eyebrow.workplace': 'Správa parkování na pracovišti',
  'session.hero.title1': 'Spravedlivé přidělování s podklady, kterým vaše firma může věřit.',
  'session.hero.body1':
    'Žádejte o sdílená místa a pracovní místa, spouštějte losování podle zásad a dejte HR přehledný provozní záznam bez odhalení soukromých údajů zaměstnanců.',
  'session.verifying': 'Ověřování relace…',

  'session.eyebrow.config': 'Konfigurace prostředí',
  'session.config.title': 'Konfigurace vyžaduje pozornost.',
  'session.config.body': 'Aplikaci se nepodařilo načíst nastavení identity potřebné pro přihlášení.',
  'session.config.errorHeading': 'Chyba konfigurace',
  'session.config.errorFallback': 'Nepodařilo se načíst /config.json.',
  'session.config.hintPrefix': 'Ujistěte se, že soubor',
  'session.config.hintSuffix': 'je zveřejněn webovým serverem a obsahuje platná nastavení OIDC.',

  'session.eyebrow.brand': 'FairSpot pro moderní pracoviště',
  'session.hero.title2': 'Spravedlivé přidělování, kterému zaměstnanci rozumí.',
  'session.hero.body2':
    'Dejte zaměstnancům jasnou odpověď, dejte HR provozní kontrolu a udržujte každé losování připravené k přezkoumání.',
  'session.legalLink': 'Právní informace',

  'session.signIn.heading': 'Přihlásit se',
  'session.signIn.secureAccessFor': 'Zabezpečený přístup pro {tenantName}.',
  'session.signIn.secureAccessDefault': 'Zabezpečený přístup pro zaměstnance i HR.',

  'session.security.fairAllocation': 'Spravedlivé přidělování',
  'session.security.teamPolicies': 'Týmové zásady',
  'session.security.clearOutcomes': 'Jasné výsledky',

  'session.dev.hide': 'Skrýt vývojářský přístup',
  'session.dev.show': 'Vývojářský přístup',
  'session.dev.note': 'Pouze pro vývoj. Vložte token z lokálního testovacího skriptu.',
  'session.dev.apiBaseUrl': 'Základní URL adresa API',
  'session.dev.bearerToken': 'Bearer token',
  'session.dev.verifying': 'Ověřování…',
  'session.dev.useToken': 'Použít token',
  'session.dev.clearToken': 'Vymazat uložený token',
  'session.dev.bothRequired': 'Obě pole jsou povinná.',

  'session.snapshot.ariaLabel': 'Provozní přehled',
  'session.snapshot.nextDraw': 'Příští losování',
  'session.snapshot.nextDrawSub': 'Okno zásad viditelné zaměstnancům',
  'session.snapshot.hrView': 'Pohled HR',
  'session.snapshot.hrViewValue': 'Živě',
  'session.snapshot.hrViewSub': 'Žádosti, výsledky a výjimky',
  'session.snapshot.evidence': 'Podklady',
  'session.snapshot.evidenceValue': 'Sledovatelné',
  'session.snapshot.evidenceSub': 'Rozhodnutí o přidělení jsou uchovávána k přezkoumání',

  'session.email.label': 'E-mail',
  'session.email.notFound':
    'Pro tento e-mail se nepodařilo najít cestu k přihlášení. Zkontrolujte adresu a zkuste to znovu, nebo se přihlaste pomocí účtu FairSpot. Pokud se to nedaří opakovaně, obraťte se na správce FairSpot ve vaší firmě.',
  'session.email.error': 'Při hledání vašeho přihlášení se něco nepovedlo. Zkuste to znovu, nebo se přihlaste pomocí účtu FairSpot.',
  'session.email.finding': 'Hledáme vaše přihlášení…',
  'session.email.routingSso': 'Přesměrováváme vás na přihlášení vaší firmy…',
  'session.email.routingLocal': 'Přesměrováváme vás na přihlášení FairSpot…',
  'session.email.continue': 'Pokračovat',
  'session.email.signInFairspot': 'Přihlásit se místo toho pomocí účtu FairSpot',

  'session.callback.completing': 'Dokončujeme přihlášení…',

  'session.verify.brandSubtitle': 'Ověření e-mailu',
  'session.verify.heading': 'Potvrďte svou e-mailovou adresu',
  'session.verify.confirming': 'Potvrzujeme vaši e-mailovou adresu…',
  'session.verify.verified': 'Vaše e-mailová adresa je potvrzena. Nyní můžete dostávat oznámení z FairSpot.',
  'session.verify.needSignIn':
    'Přihlaste se prosím do FairSpot tímto účtem a poté znovu otevřete ověřovací odkaz z e-mailu pro potvrzení. Potvrzení je vázáno na vaši vlastní přihlášenou relaci.',
  'session.verify.rejected': 'Tento ověřovací odkaz už není platný. Vyžádejte si nový ověřovací e-mail ve svém profilu FairSpot a zkuste to znovu.',
  'session.verify.rejectedExpired':
    'Tento ověřovací odkaz už není platný – jeho platnost vypršela. Vyžádejte si nový ověřovací e-mail ve svém profilu FairSpot a zkuste to znovu.',
  'session.verify.noToken': 'Tomuto odkazu chybí ověřovací kód. Otevřete odkaz přímo z e-mailu od FairSpot.',
  'session.verify.error': 'Vaši e-mailovou adresu se teď nepodařilo potvrdit. Zkuste odkaz prosím znovu za chvíli.',
  'session.verify.goToFairspot': 'Přejít do FairSpot',
};

export const sessionMessages = { en, cs };
