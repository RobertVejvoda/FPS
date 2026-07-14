// LOC001 (#744) — More tab copy: account summary, spot eligibility, my
// vehicles, about/legal, and role display labels used there.
import type { Catalog } from '../catalog';

const en = {
  'more.heading': 'More',
  'more.account.title': 'Your Account',
  'more.account.role': 'Role',
  'more.account.cannotLoad': 'Cannot load your account',

  'more.notificationPrefs.title': 'Notification preferences',
  'more.notificationPrefs.description': 'Reminder and channel preferences land in a later mobile slice.',

  'more.about.title': 'About FairSpot',
  'more.about.license': 'FairSpot is AGPL-3.0-or-later open-source software.',
  'more.about.copyright': 'Copyright © 2026 Robert Vejvoda.',
  'more.about.trademark':
    "The FairSpot name and logo identify Robert Vejvoda's project; modified or hosted forks must not imply endorsement.",
  'more.about.sourceLinkLabel': 'Open FairSpot source code',
  'more.about.sourceLinkText': 'Source code and license',

  'more.eligibility.loadingProfile': 'Loading profile…',
  'more.eligibility.unavailableTitle': 'Spot eligibility unavailable',
  'more.eligibility.unavailableMessage': 'No spot profile exists for this account yet.',
  'more.eligibility.errorTitle': 'Spot profile unavailable',
  'more.eligibility.sectionTitle': 'Spot Eligibility',
  'more.eligibility.spotEligible': 'Spot eligible',
  'more.eligibility.accessibleEligible': 'Accessible spot eligible',
  'more.eligibility.reservedEligible': 'Reserved space eligible',
  'more.eligibility.companyCarOnFile': 'Company car on file',

  'more.vehicles.title': 'My Vehicles',
  'more.vehicles.none': 'No vehicles are linked to your profile.',
  'more.vehicles.unknownPlate': 'Unknown plate',
  'more.vehicles.default': 'Default',
  'more.vehicles.electric': 'Electric',
  'more.vehicles.standard': 'Standard',
  'more.vehicles.active': 'Active',
  'more.vehicles.inactive': 'Inactive',

  // Language selector row. The row label is deliberately shown in both
  // languages at once ('Language / Jazyk') so a user in the wrong language
  // can always find the switcher — it is not looked up through the catalog.
  // Each option shows its own language's name regardless of the active
  // locale, matching standard language-picker convention.
  'more.language.english': 'English',
  'more.language.czech': 'Čeština',

  // Role display labels (src/displayLabels.ts formatRoles) — keyed by the
  // raw role string from the identity token / backend (PascalCase and
  // snake_case forms both occur).
  'labels.role.EmployeeMobile': 'Employee',
  'labels.role.Employee': 'Employee',
  'labels.role.Admin': 'Administrator',
  'labels.role.Auditor': 'Auditor',
  'labels.role.HrManager': 'HR Manager',
  'labels.role.ReportViewer': 'Report Viewer',
  'labels.role.employee': 'Employee',
  'labels.role.admin': 'Administrator',
  'labels.role.auditor': 'Auditor',
  'labels.role.hr_manager': 'HR Manager',
  'labels.role.report_viewer': 'Report Viewer',
} as const;

const cs: Catalog<keyof typeof en> = {
  'more.heading': 'Více',
  'more.account.title': 'Váš účet',
  'more.account.role': 'Role',
  'more.account.cannotLoad': 'Účet se nepodařilo načíst',

  'more.notificationPrefs.title': 'Předvolby oznámení',
  'more.notificationPrefs.description': 'Předvolby připomenutí a kanálů budou doplněny v pozdější mobilní verzi.',

  'more.about.title': 'O aplikaci FairSpot',
  'more.about.license': 'FairSpot je open-source software pod licencí AGPL-3.0-or-later.',
  'more.about.copyright': 'Copyright © 2026 Robert Vejvoda.',
  'more.about.trademark':
    'Název a logo FairSpot identifikují projekt Roberta Vejvody; upravené nebo hostované forky nesmí naznačovat jeho podporu.',
  'more.about.sourceLinkLabel': 'Otevřít zdrojový kód FairSpot',
  'more.about.sourceLinkText': 'Zdrojový kód a licence',

  'more.eligibility.loadingProfile': 'Načítání profilu…',
  'more.eligibility.unavailableTitle': 'Způsobilost pro místo není k dispozici',
  'more.eligibility.unavailableMessage': 'Pro tento účet zatím neexistuje žádný profil.',
  'more.eligibility.errorTitle': 'Profil pro místa není k dispozici',
  'more.eligibility.sectionTitle': 'Způsobilost pro místo',
  'more.eligibility.spotEligible': 'Způsobilost pro místo',
  'more.eligibility.accessibleEligible': 'Způsobilost pro bezbariérové místo',
  'more.eligibility.reservedEligible': 'Způsobilost pro vyhrazené místo',
  'more.eligibility.companyCarOnFile': 'Firemní vozidlo v profilu',

  'more.vehicles.title': 'Moje vozidla',
  'more.vehicles.none': 'K vašemu profilu nejsou přiřazena žádná vozidla.',
  'more.vehicles.unknownPlate': 'Neznámá SPZ',
  'more.vehicles.default': 'Výchozí',
  'more.vehicles.electric': 'Elektromobil',
  'more.vehicles.standard': 'Standardní',
  'more.vehicles.active': 'Aktivní',
  'more.vehicles.inactive': 'Neaktivní',

  'more.language.english': 'English',
  'more.language.czech': 'Čeština',

  'labels.role.EmployeeMobile': 'Zaměstnanec',
  'labels.role.Employee': 'Zaměstnanec',
  'labels.role.Admin': 'Správce',
  'labels.role.Auditor': 'Auditor',
  'labels.role.HrManager': 'HR manažer',
  'labels.role.ReportViewer': 'Prohlížitel přehledů',
  'labels.role.employee': 'Zaměstnanec',
  'labels.role.admin': 'Správce',
  'labels.role.auditor': 'Auditor',
  'labels.role.hr_manager': 'HR manažer',
  'labels.role.report_viewer': 'Prohlížitel přehledů',
};

export const moreMessages = { en, cs };
