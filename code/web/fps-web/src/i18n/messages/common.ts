// LOC001 (#744) — app shell, navigation, footer and generic shared copy.
//
// Catalog convention (same in every domain file): `en` is the source of
// truth, `cs` is typed against `en`'s keys so a missing translation fails
// `npm run typecheck`. Czech copy uses formal address (vykání).
const en = {
  'common.loading': 'Loading…',
  'common.language': 'Language',
  'common.notAvailable': '—',

  'nav.ariaLabel': 'Main navigation',
  'nav.myReservations': 'My Reservations',
  'nav.request': 'Request',
  'nav.profile': 'Profile',
  'nav.notifications': 'Notifications',
  'nav.parkingMap': 'Parking Map',
  'nav.parkingRequests': 'Parking Requests',
  'nav.seatRequests': 'Seat Requests',
  'nav.draws': 'Draws',
  'nav.reports': 'Reports',
  'nav.configuration': 'Configuration',
  'nav.hrImport': 'HR Import',
  'nav.auditorWorkspace': 'Auditor Workspace',
  'nav.auditConsole': 'Audit Console',
  'nav.admin': 'Admin',
  'nav.legal': 'Legal',
  'nav.signOut': 'Sign out',

  'footer.simulationBanner': 'NON-PRODUCTION SIMULATION',
  'footer.simulationBannerTitle':
    'Non-production simulation mode is active. Virtual time is being used instead of real time.',
  'footer.realTime': 'Real: {time}',
  'footer.realTimeTitle': 'Current real-world time',
  'footer.simTime': 'Sim: {time}',
  'footer.simTimeTitle': 'Current virtual/simulated time used for testing',
  'footer.simulationInactive': 'Simulation: inactive (using real time)',
  'footer.simulationLoading': 'Loading simulation clock...',
  'footer.simulationUnavailable': 'Simulation clock unavailable',
  'footer.simulationUnreachable': 'Simulation clock not reachable',
  'footer.reset': 'Reset',

  // PLAT008F (#805) — shown to platform-plane identities; the operator console
  // lives in the private platform deployment, not in this tenant app.
  'platform.moved.title': 'Operator console has moved',
  'platform.moved.body': 'This is the FairSpot tenant app. Platform operator identities use the dedicated operator console — open it at your organization’s operator console address.',
  'platform.moved.signOut': 'Sign out',
} as const;

const cs: Record<keyof typeof en, string> = {
  'common.loading': 'Načítání…',
  'common.language': 'Jazyk',
  'common.notAvailable': '—',

  'nav.ariaLabel': 'Hlavní navigace',
  'nav.myReservations': 'Moje rezervace',
  'nav.request': 'Nová žádost',
  'nav.profile': 'Profil',
  'nav.notifications': 'Oznámení',
  'nav.parkingMap': 'Mapa parkoviště',
  'nav.parkingRequests': 'Žádosti o parkování',
  'nav.seatRequests': 'Žádosti o pracovní místa',
  'nav.draws': 'Losování',
  'nav.reports': 'Přehledy',
  'nav.configuration': 'Konfigurace',
  'nav.hrImport': 'HR import',
  'nav.auditorWorkspace': 'Pracovní plocha auditora',
  'nav.auditConsole': 'Auditní konzole',
  'nav.admin': 'Správa',
  'nav.legal': 'Právní informace',
  'nav.signOut': 'Odhlásit se',

  'footer.simulationBanner': 'NEPRODUKČNÍ SIMULACE',
  'footer.simulationBannerTitle':
    'Je aktivní neprodukční simulační režim. Místo reálného času se používá virtuální čas.',
  'footer.realTime': 'Reálný čas: {time}',
  'footer.realTimeTitle': 'Aktuální reálný čas',
  'footer.simTime': 'Simulace: {time}',
  'footer.simTimeTitle': 'Aktuální virtuální (simulovaný) čas používaný pro testování',
  'footer.simulationInactive': 'Simulace: neaktivní (běží reálný čas)',
  'footer.simulationLoading': 'Načítání simulačních hodin…',
  'footer.simulationUnavailable': 'Simulační hodiny nejsou k dispozici',
  'footer.simulationUnreachable': 'Simulační hodiny nejsou dosažitelné',
  'footer.reset': 'Reset',

  'platform.moved.title': 'Konzole operátora se přesunula',
  'platform.moved.body': 'Toto je aplikace FairSpot pro nájemce. Identity operátora platformy používají samostatnou konzoli operátora — otevřete ji na adrese konzole operátora vaší organizace.',
  'platform.moved.signOut': 'Odhlásit se',
};

export const commonMessages = { en, cs };
