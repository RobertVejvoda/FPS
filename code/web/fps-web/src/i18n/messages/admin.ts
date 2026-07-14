// LOC001 (#744) — tenant admin, identity settings and configuration copy.
import type { Catalog } from '../catalog';

const en = {
  // ── shared across admin-owned pages ──────────────────────────────────────
  'admin.common.retry': 'Retry',
  'admin.common.refresh': 'Refresh',
  'admin.common.saving': 'Saving…',

  // ── TenantAdminPage ───────────────────────────────────────────────────────
  'admin.tenantAdmin.title': 'Tenant Admin',
  'admin.tenantAdmin.identityLoadError': 'Failed to load identity.',
  'admin.tenantAdmin.noAccess': 'You do not have admin access to view the tenant console.',
  'admin.tenantAdmin.overviewTitle': 'Tenant Overview',
  'admin.tenantAdmin.overviewLoadError': 'Failed to load tenant.',
  'admin.tenantAdmin.name': 'Name',
  'admin.tenantAdmin.slug': 'Slug',
  'admin.tenantAdmin.region': 'Region',
  'admin.tenantAdmin.timeZone': 'Time zone',
  'admin.tenantAdmin.primaryModule': 'Primary module',
  'admin.tenantAdmin.enabledModules': 'Enabled modules',
  'admin.tenantAdmin.lifecycle': 'Lifecycle',
  'admin.tenantAdmin.created': 'Created',
  'admin.tenantAdmin.updated': 'Updated',
  'admin.tenantAdmin.lifecycleState.Draft': 'Draft',
  'admin.tenantAdmin.lifecycleState.Configured': 'Configured',
  'admin.tenantAdmin.lifecycleState.Seeded': 'Seeded',
  'admin.tenantAdmin.lifecycleState.Ready': 'Ready',
  'admin.tenantAdmin.lifecycleState.Suspended': 'Suspended',
  'admin.tenantAdmin.lifecycleState.Archived': 'Archived',

  // ── Readiness section ─────────────────────────────────────────────────────
  'admin.readiness.title': 'Readiness',
  'admin.readiness.description':
    'Ready means the tenant is fully configured for employees to request spots, participate in draws, and receive notifications.',
  'admin.readiness.loadError': 'Failed to load readiness.',
  'admin.readiness.ready': 'Ready',
  'admin.readiness.notReady': 'Not ready',
  'admin.readiness.deferredSuffix': '(pilot deferred)',
  'admin.readiness.goTo': 'Go to {label} →',
  'admin.readiness.actionRequired': 'Action required: {label}',
  'admin.readiness.fallbackNextAction': 'Investigate and resolve the failing check.',
  'admin.readiness.pilotReady': 'Tenant is pilot-ready — required checks passed.',
  'admin.readiness.allPassed': 'All checks passed — tenant is ready for live use.',
  'admin.readiness.deferredSummary.one':
    '{count} item is deferred for the pilot and will not block day-to-day operation. Resolve before moving to production: {list}.',
  'admin.readiness.deferredSummary.other':
    '{count} items are deferred for the pilot and will not block day-to-day operation. Resolve before moving to production: {list}.',

  // Readiness check metadata (CHECK_META)
  'admin.readiness.check.LifecycleState.label': 'Tenant lifecycle',
  'admin.readiness.check.LifecycleState.purpose':
    'Tenant must be in an active lifecycle state before employees can book.',
  'admin.readiness.check.LifecycleState.nextAction':
    'Contact your FairSpot operator to advance the tenant lifecycle to Configured or Ready.',

  'admin.readiness.check.IdentityConfig.label': 'Identity & login',
  'admin.readiness.check.IdentityConfig.purpose': 'SSO login must be configured so employees can sign in.',
  'admin.readiness.check.IdentityConfig.nextAction':
    'Configure your identity provider in the Identity & Login Settings section below.',

  'admin.readiness.check.ActiveAdmin.label': 'Administrator account',
  'admin.readiness.check.ActiveAdmin.purpose': 'At least one active admin must exist to manage the tenant.',
  'admin.readiness.check.ActiveAdmin.nextAction':
    'Add an administrator account, or contact your operator to register the first admin.',

  'admin.readiness.check.RoleMapping.label': 'Role configuration',
  'admin.readiness.check.RoleMapping.purpose':
    'Roles must map to valid FairSpot roles (employee, admin, hr_manager, report_viewer, auditor).',
  'admin.readiness.check.RoleMapping.nextAction':
    'Fix the role mapping in the Identity & Login Settings section below.',

  'admin.readiness.check.ParkingPolicy.label': 'Parking policy',
  'admin.readiness.check.ParkingPolicy.purpose':
    'A default parking policy defines the draw schedule and booking rules for your employees.',
  'admin.readiness.check.ParkingPolicy.nextAction': 'Set up a default parking policy in Configuration.',

  'admin.readiness.check.ParkingLocation.label': 'Parking locations & capacity',
  'admin.readiness.check.ParkingLocation.purpose':
    'At least one location with active parking slots is required for draws to run.',
  'admin.readiness.check.ParkingLocation.nextAction': 'Add a location with at least one active slot in Configuration.',

  'admin.readiness.check.ProfileFacts.label': 'Employee data',
  'admin.readiness.check.ProfileFacts.purpose': 'Employee profiles must be loaded so staff can participate in draws.',
  'admin.readiness.check.ProfileFacts.nextAction': 'Import your employee list in HR Import.',

  'admin.readiness.check.BookingSmokeTest.label': 'Booking service',
  'admin.readiness.check.BookingSmokeTest.purpose':
    'The booking service must be available to run draws and accept spot requests.',
  'admin.readiness.check.BookingSmokeTest.nextAction':
    'Check that the booking service is running. If the issue persists, contact your operator.',

  'admin.readiness.check.NotificationReachable.label': 'Notifications',
  'admin.readiness.check.NotificationReachable.purpose':
    'The notification service must be available to inform employees of draw outcomes.',
  'admin.readiness.check.NotificationReachable.nextAction':
    'Check that the notification service is running. If the issue persists, contact your operator.',

  'admin.readiness.check.AuditEvidence.label': 'Audit trail',
  'admin.readiness.check.AuditEvidence.purpose':
    'The audit service must be available to record draw evidence and fairness logs.',
  'admin.readiness.check.AuditEvidence.nextAction':
    'Check that the audit service is running. If the issue persists, contact your operator.',

  'admin.readiness.check.ReportingEvidence.label': 'Reporting',
  'admin.readiness.check.ReportingEvidence.purpose':
    'The reporting service must be available for management and compliance reports.',
  'admin.readiness.check.ReportingEvidence.nextAction':
    'Check that the reporting service is running. If the issue persists, contact your operator.',

  'admin.readiness.check.ObjectStorageReadiness.label': 'Document & file storage',
  'admin.readiness.check.ObjectStorageReadiness.purpose':
    'Tenant file storage enables document uploads, report exports, audit evidence, and branding assets.',
  'admin.readiness.check.ObjectStorageReadiness.nextAction':
    'Deferred for pilot — file storage provisioning (OPS008C) is not yet implemented. FairSpot will operate without document uploads or branding assets during the pilot.',

  'admin.readiness.check.BrandingReadiness.label': 'Organization branding',
  'admin.readiness.check.BrandingReadiness.purpose':
    'Tenant branding lets your employees see your organization name and logo in FairSpot.',
  'admin.readiness.check.BrandingReadiness.nextAction':
    'Deferred for pilot — branding configuration (CUST010) is not yet implemented. FairSpot defaults will be shown to employees during the pilot.',

  // ── TenantIdentitySettingsSection ────────────────────────────────────────
  'admin.identity.title': 'Identity & Login Settings',
  'admin.identity.description.part1':
    'How your employees sign in. Company SSO settings come from your identity provider; the sign-in screen passes the entered email as a prefill hint (',
  'admin.identity.description.part2':
    '). Nothing here grants access by itself — access always comes from validated sign-in tokens.',
  'admin.identity.notConfigured':
    'Identity is not configured yet. Fill in the settings below to enable employee sign-in for this tenant.',
  'admin.identity.issuer.label': 'Trusted issuer',
  'admin.identity.issuer.hint': 'The token issuer URL your identity provider uses (from your IdP or FairSpot operator).',
  'admin.identity.audience.label': 'Audience',
  'admin.identity.audience.hint': 'The API audience expected in sign-in tokens.',
  'admin.identity.tenantClaim.label': 'Tenant claim name',
  'admin.identity.tenantClaim.hint': 'Token claim carrying the tenant.',
  'admin.identity.subjectClaim.label': 'Subject claim name',
  'admin.identity.subjectClaim.hint': 'Token claim carrying the stable user id.',
  'admin.identity.roleClaimNames.label': 'Role claim names',
  'admin.identity.roleClaimNames.hint': 'Comma-separated token claims to read groups/roles from (e.g. groups, roles).',
  'admin.identity.roleMapping.label': 'Role mapping',
  'admin.identity.roleMapping.hint':
    'One mapping per line: "idp-group = fairspot-role". Valid FairSpot roles: employee, admin, hr_manager, report_viewer, auditor. Unmapped groups are ignored.',
  'admin.identity.brokerAlias.label': 'SSO broker alias (optional)',
  'admin.identity.brokerAlias.hint':
    'Non-secret routing hint for company SSO: the Keycloak identity-provider broker alias for your company IdP. When set (and your login mode is Company SSO), the sign-in screen sends it as kc_idp_hint so employees skip the account chooser and go straight to your IdP. This is routing metadata only — never a client secret, and it does not grant access. Leave empty if no external IdP broker is configured.',
  'admin.identity.localAccounts.label': 'Allow FairSpot-local accounts',
  'admin.identity.localAccounts.description':
    'Permits local fallback and break-glass accounts for this tenant. Tenants using the combined login mode keep the standard sign-in screen, so local accounts stay reachable.',
  'admin.identity.saveError': 'Saving failed: {message} Try again.',
  'admin.identity.saved': 'Identity settings saved. Readiness is being refreshed.',
  'admin.identity.save': 'Save identity settings',
  'admin.identity.discard': 'Discard changes',

  // identityConfigForm.ts validation messages
  'admin.identity.validation.issuerRequired': 'Trusted issuer is required.',
  'admin.identity.validation.audienceRequired': 'Audience is required.',
  'admin.identity.validation.tenantClaimRequired':
    'Tenant claim name is required. Use "tenant_id" unless your identity provider issues a different claim.',
  'admin.identity.validation.subjectClaimRequired':
    'Subject claim name is required. Use "sub" unless your identity provider issues a different claim.',
  'admin.identity.validation.mappingLineFormat':
    'Each mapping line must look like "idp-group = fairspot-role" (line: "{line}").',
  'admin.identity.validation.duplicateGroup':
    'The group "{key}" is mapped more than once. Keep one line per group.',

  // ── ConfigurationPage ─────────────────────────────────────────────────────
  'admin.configuration.title': 'Configuration',
  'admin.configuration.loadingPolicy': 'Loading policy…',
  'admin.configuration.forbidden': 'You do not have permission to view or edit configuration.',
  'admin.configuration.loadPolicyError': 'Failed to load policy.',
  'admin.configuration.policySaved': 'Policy saved.',
  'admin.configuration.savePolicyError': 'Failed to save policy.',
  'admin.configuration.savePolicy': 'Save policy',
  'admin.configuration.noLocationPolicy': 'No location policy.',
  'admin.configuration.locationHistoryError': 'Failed to load location history.',
  'admin.configuration.slotsLoadError': 'Failed to load slots.',
  'admin.configuration.locationPolicySaved': 'Location policy saved.',
  'admin.configuration.saveLocationPolicyError': 'Failed to save location policy.',
  'admin.configuration.saveLocationPolicy': 'Save location policy',
  'admin.configuration.slotsSaved': 'Slots saved.',
  'admin.configuration.saveSlotsError': 'Failed to save slots.',
  'admin.configuration.saveSlots': 'Save slots',
  'admin.configuration.tenantHistoryError': 'Failed to load history.',
  'admin.configuration.demoDraw.resultAlreadyCompleted':
    'Draw already completed: {allocated} allocated, {rejected} rejected, {waitlisted} waitlisted.',
  'admin.configuration.demoDraw.resultCompleted':
    'Draw completed: {allocated} allocated, {rejected} rejected, {waitlisted} waitlisted.',
  'admin.configuration.demoDrawFailed': 'Draw failed.',
  'admin.configuration.tenantPolicyTitle': 'Tenant Parking Policy',
  'admin.configuration.version': 'Version: {version}',
  'admin.configuration.demoDrawTitle': 'Demo Draw',
  'admin.configuration.locationLabel': 'Location',
  'admin.configuration.parkingDate': 'Parking date',
  'admin.configuration.arrivalTime': 'Arrival time',
  'admin.configuration.departureTime': 'Departure time',
  'admin.configuration.reason': 'Reason',
  'admin.configuration.changeReasonPlaceholder': 'Change reason (optional)',
  'admin.configuration.runningDraw': 'Running Draw…',
  'admin.configuration.runDrawNow': 'Run Draw now',
  'admin.configuration.demoDrawNote':
    'Runs one explicit Draw key. Re-running the same location, date, and time slot returns the completed result without reallocating.',
  'admin.configuration.tenantHistoryTitle': 'Tenant Policy Version History',
  'admin.configuration.noHistory': 'No history yet.',
  'admin.configuration.locationConfigTitle': 'Location Configuration',
  'admin.configuration.slotsCount.one': '{count} slot',
  'admin.configuration.slotsCount.other': '{count} slots',
  'admin.configuration.loadingLocationPolicy': 'Loading location policy…',
  'admin.configuration.locationForbidden': 'Insufficient permissions for this location.',
  'admin.configuration.locationVersion': 'Location: {location} · Version: {version}',
  'admin.configuration.locationHistoryTitle': 'Location Policy History',
  'admin.configuration.noLocationHistory': 'No location policy history yet.',
  'admin.configuration.loadingSlots': 'Loading slots…',
  'admin.configuration.slotsHeading': 'Slots ({count})',
  'admin.configuration.noSlots': 'No slots configured.',
  'admin.configuration.slotHistoryTitle': 'Slot History',
  'admin.configuration.slotTable.slotId': 'Slot ID',
  'admin.configuration.slotTable.active': 'Active',
  'admin.configuration.slotTable.charger': 'Charger',
  'admin.configuration.slotTable.accessible': 'Accessible',
  'admin.configuration.slotTable.companyCar': 'Company car',
  'admin.configuration.slotTable.moto': 'Moto',
  'admin.configuration.slotTable.motoUnits': 'Moto units',
  'admin.configuration.slotTable.reservedFor': 'Reserved for',
  'admin.configuration.slotTable.motoDefaultTitle': 'Defaults to 4 when blank',
  'admin.configuration.slotTable.motoOnlyTitle': 'Only used for motorcycle-specific slots',
  'admin.configuration.slotHistoryTable.version': 'Version',
  'admin.configuration.slotHistoryTable.changedAt': 'Changed at',
  'admin.configuration.slotHistoryTable.changedBy': 'Changed by',
  'admin.configuration.slotHistoryTable.reason': 'Reason',
  'admin.configuration.slotHistoryTable.count': 'Count',
  'admin.configuration.historyTable.version': 'Version',
  'admin.configuration.historyTable.publishedAt': 'Published at',
  'admin.configuration.historyTable.publishedBy': 'Published by',
  'admin.configuration.historyTable.reason': 'Reason',
  'admin.configuration.adminActor': 'Admin ·{ref}',

  'admin.configuration.policy.timeZone': 'Time zone',
  'admin.configuration.policy.drawCutOffTime': 'Draw cut-off time',
  'admin.configuration.policy.dailyRequestCap': 'Daily request cap',
  'admin.configuration.policy.allocationLookbackDays': 'Allocation lookback days',
  'admin.configuration.policy.lateCancellationPenalty': 'Late cancellation penalty',
  'admin.configuration.policy.noShowPenalty': 'No-show penalty',
  'admin.configuration.policy.usageConfirmationWindowMinutes': 'Usage confirmation window (min)',
  'admin.configuration.policy.manualAdjustmentEnabled': 'Manual adjustment enabled',
  'admin.configuration.policy.sameDayBookingEnabled': 'Same-day booking enabled',
  'admin.configuration.policy.sameDayUsesRequestCap': 'Same-day uses request cap',
  'admin.configuration.policy.automaticReallocationEnabled': 'Automatic reallocation',
  'admin.configuration.policy.usageConfirmationRequired': 'Usage confirmation required',
  'admin.configuration.policy.noShowDetectionEnabled': 'No-show detection enabled',
  'admin.configuration.policy.companyCarTier1Enabled': 'Company car tier 1 enabled',

  'admin.configuration.companyCar.covered': 'Company-car capacity covered',
  'admin.configuration.companyCar.exceeded': 'Company-car capacity exceeded',
  'admin.configuration.companyCar.employeeCount.one': '{count} company-car employee assigned to this location',
  'admin.configuration.companyCar.employeeCount.other': '{count} company-car employees assigned to this location',
  'admin.configuration.companyCar.slotCount.one': '{count} active fixed slot reserved for a specific user',
  'admin.configuration.companyCar.slotCount.other': '{count} active fixed slots reserved for a specific user',
  'admin.configuration.companyCar.allCovered': 'Every assigned employee has a guaranteed slot.',
  'admin.configuration.companyCar.someUncovered.one':
    '{count} employee has no guaranteed slot and will rely on the normal draw.',
  'admin.configuration.companyCar.someUncovered.other':
    '{count} employees have no guaranteed slot and will rely on the normal draw.',
} as const;

const cs: Catalog<keyof typeof en> = {
  'admin.common.retry': 'Zkusit znovu',
  'admin.common.refresh': 'Obnovit',
  'admin.common.saving': 'Ukládání…',

  'admin.tenantAdmin.title': 'Správa organizace',
  'admin.tenantAdmin.identityLoadError': 'Načtení identity se nezdařilo.',
  'admin.tenantAdmin.noAccess': 'Nemáte oprávnění správce pro zobrazení konzole organizace.',
  'admin.tenantAdmin.overviewTitle': 'Přehled organizace',
  'admin.tenantAdmin.overviewLoadError': 'Načtení organizace se nezdařilo.',
  'admin.tenantAdmin.name': 'Název',
  'admin.tenantAdmin.slug': 'Slug',
  'admin.tenantAdmin.region': 'Region',
  'admin.tenantAdmin.timeZone': 'Časové pásmo',
  'admin.tenantAdmin.primaryModule': 'Hlavní modul',
  'admin.tenantAdmin.enabledModules': 'Aktivní moduly',
  'admin.tenantAdmin.lifecycle': 'Životní cyklus',
  'admin.tenantAdmin.created': 'Vytvořeno',
  'admin.tenantAdmin.updated': 'Aktualizováno',
  'admin.tenantAdmin.lifecycleState.Draft': 'Koncept',
  'admin.tenantAdmin.lifecycleState.Configured': 'Nakonfigurováno',
  'admin.tenantAdmin.lifecycleState.Seeded': 'Inicializováno',
  'admin.tenantAdmin.lifecycleState.Ready': 'Připraveno',
  'admin.tenantAdmin.lifecycleState.Suspended': 'Pozastaveno',
  'admin.tenantAdmin.lifecycleState.Archived': 'Archivováno',

  'admin.readiness.title': 'Připravenost',
  'admin.readiness.description':
    'Připraveno znamená, že organizace je plně nakonfigurována, aby zaměstnanci mohli žádat o místa, účastnit se losování a dostávat oznámení.',
  'admin.readiness.loadError': 'Načtení připravenosti se nezdařilo.',
  'admin.readiness.ready': 'Připraveno',
  'admin.readiness.notReady': 'Nepřipraveno',
  'admin.readiness.deferredSuffix': '(odloženo pro pilotní provoz)',
  'admin.readiness.goTo': 'Přejít na {label} →',
  'admin.readiness.actionRequired': 'Vyžadována akce: {label}',
  'admin.readiness.fallbackNextAction': 'Prošetřete a vyřešte neúspěšnou kontrolu.',
  'admin.readiness.pilotReady': 'Organizace je připravena na pilotní provoz — požadované kontroly byly úspěšné.',
  'admin.readiness.allPassed': 'Všechny kontroly byly úspěšné — organizace je připravena k ostrému provozu.',
  'admin.readiness.deferredSummary.one':
    '{count} položka je odložena pro pilotní provoz a nebude blokovat běžný provoz. Vyřešte před přechodem do produkce: {list}.',
  'admin.readiness.deferredSummary.few':
    '{count} položky jsou odloženy pro pilotní provoz a nebudou blokovat běžný provoz. Vyřešte před přechodem do produkce: {list}.',
  'admin.readiness.deferredSummary.other':
    '{count} položek je odloženo pro pilotní provoz a nebude blokovat běžný provoz. Vyřešte před přechodem do produkce: {list}.',

  'admin.readiness.check.LifecycleState.label': 'Životní cyklus organizace',
  'admin.readiness.check.LifecycleState.purpose':
    'Organizace musí být v aktivním stavu životního cyklu, než mohou zaměstnanci rezervovat.',
  'admin.readiness.check.LifecycleState.nextAction':
    'Kontaktujte svého operátora FairSpot a požádejte o posun životního cyklu organizace do stavu Nakonfigurováno nebo Připraveno.',

  'admin.readiness.check.IdentityConfig.label': 'Identita a přihlášení',
  'admin.readiness.check.IdentityConfig.purpose': 'Přihlášení SSO musí být nakonfigurováno, aby se zaměstnanci mohli přihlásit.',
  'admin.readiness.check.IdentityConfig.nextAction':
    'Nakonfigurujte poskytovatele identity v sekci Nastavení identity a přihlášení níže.',

  'admin.readiness.check.ActiveAdmin.label': 'Účet správce',
  'admin.readiness.check.ActiveAdmin.purpose': 'Pro správu organizace musí existovat alespoň jeden aktivní správce.',
  'admin.readiness.check.ActiveAdmin.nextAction':
    'Přidejte účet správce, nebo kontaktujte svého operátora a požádejte o registraci prvního správce.',

  'admin.readiness.check.RoleMapping.label': 'Konfigurace rolí',
  'admin.readiness.check.RoleMapping.purpose':
    'Role musí být namapovány na platné role FairSpot (employee, admin, hr_manager, report_viewer, auditor).',
  'admin.readiness.check.RoleMapping.nextAction':
    'Opravte mapování rolí v sekci Nastavení identity a přihlášení níže.',

  'admin.readiness.check.ParkingPolicy.label': 'Zásady parkování',
  'admin.readiness.check.ParkingPolicy.purpose':
    'Výchozí zásady parkování určují plán losování a pravidla rezervací pro vaše zaměstnance.',
  'admin.readiness.check.ParkingPolicy.nextAction': 'Nastavte výchozí zásady parkování v Konfiguraci.',

  'admin.readiness.check.ParkingLocation.label': 'Lokality a kapacita parkování',
  'admin.readiness.check.ParkingLocation.purpose':
    'Pro spuštění losování je nutná alespoň jedna lokalita s aktivními parkovacími místy.',
  'admin.readiness.check.ParkingLocation.nextAction': 'Přidejte lokalitu s alespoň jedním aktivním místem v Konfiguraci.',

  'admin.readiness.check.ProfileFacts.label': 'Data zaměstnanců',
  'admin.readiness.check.ProfileFacts.purpose': 'Profily zaměstnanců musí být načteny, aby se mohli účastnit losování.',
  'admin.readiness.check.ProfileFacts.nextAction': 'Naimportujte seznam zaměstnanců v HR importu.',

  'admin.readiness.check.BookingSmokeTest.label': 'Služba rezervací',
  'admin.readiness.check.BookingSmokeTest.purpose':
    'Služba rezervací musí být dostupná pro spouštění losování a příjem žádostí o místa.',
  'admin.readiness.check.BookingSmokeTest.nextAction':
    'Zkontrolujte, že služba rezervací běží. Pokud potíže přetrvávají, kontaktujte svého operátora.',

  'admin.readiness.check.NotificationReachable.label': 'Oznámení',
  'admin.readiness.check.NotificationReachable.purpose':
    'Služba oznámení musí být dostupná, aby informovala zaměstnance o výsledcích losování.',
  'admin.readiness.check.NotificationReachable.nextAction':
    'Zkontrolujte, že služba oznámení běží. Pokud potíže přetrvávají, kontaktujte svého operátora.',

  'admin.readiness.check.AuditEvidence.label': 'Auditní stopa',
  'admin.readiness.check.AuditEvidence.purpose':
    'Auditní služba musí být dostupná pro zaznamenávání důkazů o losování a záznamů férovosti.',
  'admin.readiness.check.AuditEvidence.nextAction':
    'Zkontrolujte, že auditní služba běží. Pokud potíže přetrvávají, kontaktujte svého operátora.',

  'admin.readiness.check.ReportingEvidence.label': 'Přehledy',
  'admin.readiness.check.ReportingEvidence.purpose':
    'Služba přehledů musí být dostupná pro manažerské a kontrolní přehledy.',
  'admin.readiness.check.ReportingEvidence.nextAction':
    'Zkontrolujte, že služba přehledů běží. Pokud potíže přetrvávají, kontaktujte svého operátora.',

  'admin.readiness.check.ObjectStorageReadiness.label': 'Úložiště dokumentů a souborů',
  'admin.readiness.check.ObjectStorageReadiness.purpose':
    'Úložiště souborů organizace umožňuje nahrávání dokumentů, export přehledů, auditní důkazy a materiály značky.',
  'admin.readiness.check.ObjectStorageReadiness.nextAction':
    'Odloženo pro pilotní provoz — zajištění úložiště souborů (OPS008C) zatím není implementováno. Po dobu pilotního provozu bude FairSpot fungovat bez nahrávání dokumentů a materiálů značky.',

  'admin.readiness.check.BrandingReadiness.label': 'Vizuální styl organizace',
  'admin.readiness.check.BrandingReadiness.purpose':
    'Vizuální styl organizace umožňuje zaměstnancům vidět název a logo vaší organizace ve FairSpot.',
  'admin.readiness.check.BrandingReadiness.nextAction':
    'Odloženo pro pilotní provoz — konfigurace vizuálního stylu (CUST010) zatím není implementována. Po dobu pilotního provozu se zaměstnancům zobrazí výchozí vzhled FairSpot.',

  'admin.identity.title': 'Nastavení identity a přihlášení',
  'admin.identity.description.part1':
    'Jak se vaši zaměstnanci přihlašují. Nastavení firemního SSO pochází od vašeho poskytovatele identity; přihlašovací obrazovka předává zadaný e-mail jako předvyplněnou nápovědu (',
  'admin.identity.description.part2':
    '). Samo o sobě nic zde neuděluje přístup — přístup vždy vychází z ověřených přihlašovacích tokenů.',
  'admin.identity.notConfigured':
    'Identita zatím není nakonfigurována. Vyplňte nastavení níže a povolte tak přihlašování zaměstnanců pro tuto organizaci.',
  'admin.identity.issuer.label': 'Důvěryhodný vydavatel',
  'admin.identity.issuer.hint':
    'URL vydavatele tokenů používaná vaším poskytovatelem identity (od vašeho IdP nebo operátora FairSpot).',
  'admin.identity.audience.label': 'Audience',
  'admin.identity.audience.hint': 'Cílová skupina rozhraní API (audience) očekávaná v přihlašovacích tokenech.',
  'admin.identity.tenantClaim.label': 'Název claimu organizace',
  'admin.identity.tenantClaim.hint': 'Claim tokenu nesoucí organizaci.',
  'admin.identity.subjectClaim.label': 'Název claimu subjektu',
  'admin.identity.subjectClaim.hint': 'Claim tokenu nesoucí stabilní ID uživatele.',
  'admin.identity.roleClaimNames.label': 'Názvy claimů rolí',
  'admin.identity.roleClaimNames.hint':
    'Claimy tokenu oddělené čárkou, ze kterých se čtou skupiny/role (např. groups, roles).',
  'admin.identity.roleMapping.label': 'Mapování rolí',
  'admin.identity.roleMapping.hint':
    'Jeden řádek na mapování: "idp-group = fairspot-role". Platné role FairSpot: employee, admin, hr_manager, report_viewer, auditor. Nenamapované skupiny se ignorují.',
  'admin.identity.brokerAlias.label': 'Alias brokera SSO (volitelné)',
  'admin.identity.brokerAlias.hint':
    'Neveřejná směrovací nápověda pro firemní SSO: alias brokera poskytovatele identity Keycloak pro vašeho firemního poskytovatele identity (IdP). Pokud je nastaven (a váš režim přihlášení je Firemní SSO), přihlašovací obrazovka jej odešle jako kc_idp_hint, takže zaměstnanci přeskočí výběr účtu a přejdou přímo k vašemu IdP. Jedná se pouze o směrovací metadata — nikdy ne o klientský tajný klíč — a samo o sobě neuděluje přístup. Ponechte prázdné, pokud není nakonfigurován žádný externí broker IdP.',
  'admin.identity.localAccounts.label': 'Povolit lokální účty FairSpot',
  'admin.identity.localAccounts.description':
    'Umožňuje pro tuto organizaci lokální záložní a nouzové (break-glass) účty. Organizace používající kombinovaný režim přihlášení zachovávají standardní přihlašovací obrazovku, takže lokální účty zůstávají dostupné.',
  'admin.identity.saveError': 'Uložení se nezdařilo: {message} Zkuste to znovu.',
  'admin.identity.saved': 'Nastavení identity bylo uloženo. Připravenost se aktualizuje.',
  'admin.identity.save': 'Uložit nastavení identity',
  'admin.identity.discard': 'Zahodit změny',

  'admin.identity.validation.issuerRequired': 'Důvěryhodný vydavatel je povinný.',
  'admin.identity.validation.audienceRequired': 'Audience je povinná.',
  'admin.identity.validation.tenantClaimRequired':
    'Název claimu organizace je povinný. Použijte "tenant_id", pokud váš poskytovatel identity nevydává jiný claim.',
  'admin.identity.validation.subjectClaimRequired':
    'Název claimu subjektu je povinný. Použijte "sub", pokud váš poskytovatel identity nevydává jiný claim.',
  'admin.identity.validation.mappingLineFormat':
    'Každý řádek mapování musí mít tvar "idp-group = fairspot-role" (řádek: "{line}").',
  'admin.identity.validation.duplicateGroup':
    'Skupina "{key}" je namapována vícekrát. Ponechte pro každou skupinu jeden řádek.',

  'admin.configuration.title': 'Konfigurace',
  'admin.configuration.loadingPolicy': 'Načítání zásad…',
  'admin.configuration.forbidden': 'Nemáte oprávnění k zobrazení nebo úpravě konfigurace.',
  'admin.configuration.loadPolicyError': 'Načtení zásad se nezdařilo.',
  'admin.configuration.policySaved': 'Zásady byly uloženy.',
  'admin.configuration.savePolicyError': 'Uložení zásad se nezdařilo.',
  'admin.configuration.savePolicy': 'Uložit zásady',
  'admin.configuration.noLocationPolicy': 'Zásady pro lokalitu nejsou k dispozici.',
  'admin.configuration.locationHistoryError': 'Načtení historie lokality se nezdařilo.',
  'admin.configuration.slotsLoadError': 'Načtení míst se nezdařilo.',
  'admin.configuration.locationPolicySaved': 'Zásady lokality byly uloženy.',
  'admin.configuration.saveLocationPolicyError': 'Uložení zásad lokality se nezdařilo.',
  'admin.configuration.saveLocationPolicy': 'Uložit zásady lokality',
  'admin.configuration.slotsSaved': 'Místa byla uložena.',
  'admin.configuration.saveSlotsError': 'Uložení míst se nezdařilo.',
  'admin.configuration.saveSlots': 'Uložit místa',
  'admin.configuration.tenantHistoryError': 'Načtení historie se nezdařilo.',
  'admin.configuration.demoDraw.resultAlreadyCompleted':
    'Losování už bylo dokončeno: přiděleno {allocated}, zamítnuto {rejected}, na čekací listině {waitlisted}.',
  'admin.configuration.demoDraw.resultCompleted':
    'Losování dokončeno: přiděleno {allocated}, zamítnuto {rejected}, na čekací listině {waitlisted}.',
  'admin.configuration.demoDrawFailed': 'Losování se nezdařilo.',
  'admin.configuration.tenantPolicyTitle': 'Zásady parkování organizace',
  'admin.configuration.version': 'Verze: {version}',
  'admin.configuration.demoDrawTitle': 'Ukázkové losování',
  'admin.configuration.locationLabel': 'Lokalita',
  'admin.configuration.parkingDate': 'Datum parkování',
  'admin.configuration.arrivalTime': 'Čas příjezdu',
  'admin.configuration.departureTime': 'Čas odjezdu',
  'admin.configuration.reason': 'Důvod',
  'admin.configuration.changeReasonPlaceholder': 'Důvod změny (volitelné)',
  'admin.configuration.runningDraw': 'Losování probíhá…',
  'admin.configuration.runDrawNow': 'Spustit losování',
  'admin.configuration.demoDrawNote':
    'Spustí jeden explicitní klíč losování. Opakované spuštění se stejnou lokalitou, datem a časovým oknem vrátí dokončený výsledek bez nového přidělování.',
  'admin.configuration.tenantHistoryTitle': 'Historie verzí zásad organizace',
  'admin.configuration.noHistory': 'Zatím žádná historie.',
  'admin.configuration.locationConfigTitle': 'Konfigurace lokality',
  'admin.configuration.slotsCount.one': '{count} místo',
  'admin.configuration.slotsCount.few': '{count} místa',
  'admin.configuration.slotsCount.other': '{count} míst',
  'admin.configuration.loadingLocationPolicy': 'Načítání zásad lokality…',
  'admin.configuration.locationForbidden': 'Nedostatečná oprávnění pro tuto lokalitu.',
  'admin.configuration.locationVersion': 'Lokalita: {location} · Verze: {version}',
  'admin.configuration.locationHistoryTitle': 'Historie zásad lokality',
  'admin.configuration.noLocationHistory': 'Zatím žádná historie zásad lokality.',
  'admin.configuration.loadingSlots': 'Načítání míst…',
  'admin.configuration.slotsHeading': 'Místa ({count})',
  'admin.configuration.noSlots': 'Nejsou nakonfigurována žádná místa.',
  'admin.configuration.slotHistoryTitle': 'Historie míst',
  'admin.configuration.slotTable.slotId': 'ID místa',
  'admin.configuration.slotTable.active': 'Aktivní',
  'admin.configuration.slotTable.charger': 'Nabíječka',
  'admin.configuration.slotTable.accessible': 'Bezbariérové',
  'admin.configuration.slotTable.companyCar': 'Služební vůz',
  'admin.configuration.slotTable.moto': 'Moto',
  'admin.configuration.slotTable.motoUnits': 'Jednotky moto',
  'admin.configuration.slotTable.reservedFor': 'Vyhrazeno pro',
  'admin.configuration.slotTable.motoDefaultTitle': 'Prázdné pole znamená výchozí hodnotu 4',
  'admin.configuration.slotTable.motoOnlyTitle': 'Používá se pouze pro místa vyhrazená pro motocykly',
  'admin.configuration.slotHistoryTable.version': 'Verze',
  'admin.configuration.slotHistoryTable.changedAt': 'Změněno',
  'admin.configuration.slotHistoryTable.changedBy': 'Změnil(a)',
  'admin.configuration.slotHistoryTable.reason': 'Důvod',
  'admin.configuration.slotHistoryTable.count': 'Počet',
  'admin.configuration.historyTable.version': 'Verze',
  'admin.configuration.historyTable.publishedAt': 'Publikováno',
  'admin.configuration.historyTable.publishedBy': 'Publikoval(a)',
  'admin.configuration.historyTable.reason': 'Důvod',
  'admin.configuration.adminActor': 'Správce ·{ref}',

  'admin.configuration.policy.timeZone': 'Časové pásmo',
  'admin.configuration.policy.drawCutOffTime': 'Uzávěrka losování',
  'admin.configuration.policy.dailyRequestCap': 'Denní limit žádostí',
  'admin.configuration.policy.allocationLookbackDays': 'Dny zpětného pohledu při přidělování',
  'admin.configuration.policy.lateCancellationPenalty': 'Penalizace za pozdní zrušení',
  'admin.configuration.policy.noShowPenalty': 'Penalizace za nedostavení se',
  'admin.configuration.policy.usageConfirmationWindowMinutes': 'Okno pro potvrzení využití (min)',
  'admin.configuration.policy.manualAdjustmentEnabled': 'Ruční úpravy povoleny',
  'admin.configuration.policy.sameDayBookingEnabled': 'Rezervace v tentýž den povolena',
  'admin.configuration.policy.sameDayUsesRequestCap': 'Rezervace v tentýž den využívá limit žádostí',
  'admin.configuration.policy.automaticReallocationEnabled': 'Automatické přerozdělení',
  'admin.configuration.policy.usageConfirmationRequired': 'Vyžadováno potvrzení využití',
  'admin.configuration.policy.noShowDetectionEnabled': 'Detekce nedostavení se povolena',
  'admin.configuration.policy.companyCarTier1Enabled': 'Povolena úroveň 1 pro služební vozy',

  'admin.configuration.companyCar.covered': 'Kapacita pro služební vozy pokryta',
  'admin.configuration.companyCar.exceeded': 'Kapacita pro služební vozy překročena',
  'admin.configuration.companyCar.employeeCount.one': '{count} zaměstnanec se služebním vozem přiřazený k této lokalitě',
  'admin.configuration.companyCar.employeeCount.few': '{count} zaměstnanci se služebním vozem přiřazení k této lokalitě',
  'admin.configuration.companyCar.employeeCount.other': '{count} zaměstnanců se služebním vozem přiřazených k této lokalitě',
  'admin.configuration.companyCar.slotCount.one': '{count} aktivní pevné místo vyhrazené pro konkrétního uživatele',
  'admin.configuration.companyCar.slotCount.few': '{count} aktivní pevná místa vyhrazená pro konkrétního uživatele',
  'admin.configuration.companyCar.slotCount.other': '{count} aktivních pevných míst vyhrazených pro konkrétního uživatele',
  'admin.configuration.companyCar.allCovered': 'Každý přiřazený zaměstnanec má garantované místo.',
  'admin.configuration.companyCar.someUncovered.one':
    '{count} zaměstnanec nemá garantované místo a bude se spoléhat na běžné losování.',
  'admin.configuration.companyCar.someUncovered.few':
    '{count} zaměstnanci nemají garantované místo a budou se spoléhat na běžné losování.',
  'admin.configuration.companyCar.someUncovered.other':
    '{count} zaměstnanců nemá garantované místo a bude se spoléhat na běžné losování.',
};

export const adminMessages = { en, cs };
