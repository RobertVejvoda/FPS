// LOC001 (#744) — reporting page copy.
import type { Catalog } from '../catalog';

const en = {
  'reporting.error.dashboard': 'Failed to load dashboard.',
  'reporting.error.summary': 'Failed to load summary.',
  'reporting.error.fairness': 'Failed to load fairness.',
  'reporting.error.utilization': 'Failed to load utilization.',
  'reporting.error.reasonCodes': 'Failed to load reason codes.',
  'reporting.error.employeeImpact': 'Failed to load employee impact.',
  'reporting.error.operationalExceptions': 'Failed to load operational exceptions.',

  'reporting.unknownRequestor': 'Unknown requestor · {ref}',

  'reporting.loadingReport': 'Loading report…',
  'reporting.forbiddenTitle': 'Reporting data is not available for your current role.',
  'reporting.forbiddenDetail': 'HR and administrator accounts can access reports. If you expect access, contact your administrator.',
  'reporting.retry': 'Retry',

  'reporting.title': 'Parking Reports',
  'reporting.downloading': 'Downloading…',
  'reporting.outcomesCsv': 'Allocation outcomes CSV',
  'reporting.summaryCsv': 'Download summary CSV',

  'reporting.noDataTitle': 'No report data yet',
  'reporting.noDataDetail':
    'Reports are generated from completed allocation draws. Run a draw from HR Operations to see allocation outcomes, rejection reasons, and utilization data here.',

  'reporting.stat.totalDemand': 'Total demand',
  'reporting.stat.allocations': 'Allocations',
  'reporting.stat.allocationRate': 'Allocation rate',
  'reporting.stat.rejections': 'Rejections',
  'reporting.stat.cancellations': 'Cancellations',
  'reporting.stat.noShows': 'No-shows',

  'reporting.rejectionsByReason': 'Rejections By Reason',

  'reporting.dailyTrend': 'Daily Trend',
  'reporting.dailyTrendTable.date': 'Date',
  'reporting.dailyTrendTable.demand': 'Demand',
  'reporting.dailyTrendTable.allocations': 'Allocations',
  'reporting.dailyTrendTable.rate': 'Rate',

  'reporting.utilizationByLocation': 'Utilization By Location',
  'reporting.utilizationTable.location': 'Location',
  'reporting.utilizationTable.demand': 'Demand',
  'reporting.utilizationTable.allocated': 'Allocated',
  'reporting.utilizationTable.rate': 'Rate',
  'reporting.utilizationTable.rejected': 'Rejected',
  'reporting.utilizationTable.cancelled': 'Cancelled',
  'reporting.utilizationTable.noShows': 'No-shows',
  'reporting.utilizationError': 'Utilization: {message}',

  'reporting.reasonCodes': 'Reason Codes',
  'reporting.totalDemand': 'Total demand: {count}',
  'reporting.reasonCodeTable.reason': 'Reason',
  'reporting.reasonCodeTable.count': 'Count',
  'reporting.reasonCodeTable.percentOfDemand': '% of demand',
  'reporting.reasonCodesError': 'Reason codes: {message}',

  'reporting.dailySummary': 'Daily Summary',
  'reporting.summaryTable.date': 'Date',
  'reporting.summaryTable.location': 'Location',
  'reporting.summaryTable.slot': 'Slot',
  'reporting.summaryTable.demand': 'Demand',
  'reporting.summaryTable.alloc': 'Alloc',
  'reporting.summaryTable.rate': 'Rate',
  'reporting.summaryTable.rejected': 'Rejected',
  'reporting.summaryTable.cancelled': 'Cancelled',
  'reporting.summaryTable.noShows': 'No-shows',
  'reporting.summaryError': 'Summary: {message}',

  'reporting.fairness': 'Fairness',
  'reporting.fairnessTable.requestor': 'Requestor',
  'reporting.fairnessTable.requests': 'Requests',
  'reporting.fairnessTable.allocations': 'Allocations',
  'reporting.fairnessTable.rate': 'Rate',
  'reporting.fairnessError': 'Fairness: {message}',

  'reporting.employeeImpact': 'Employee Impact Summary',
  'reporting.employeeImpactSubtitle':
    'Employees with {threshold}+ rejections in the selected period (pseudonymized for privacy)',
  'reporting.employeeImpactTable.requestor': 'Requestor',
  'reporting.employeeImpactTable.totalRequests': 'Total Requests',
  'reporting.employeeImpactTable.rejections': 'Rejections',
  'reporting.employeeImpactTable.allocations': 'Allocations',
  'reporting.employeeImpactError': 'Employee impact: {message}',

  'reporting.operationalExceptions': 'Operational Exceptions',
  'reporting.operationalExceptionsSubtitle': 'Dates with draw failures, missing allocations, or fully rejected demand.',
  'reporting.exceptionsTable.date': 'Date',
  'reporting.exceptionsTable.location': 'Location',
  'reporting.exceptionsTable.issue': 'Issue',
  'reporting.exceptionsTable.demand': 'Demand',
  'reporting.exceptionsTable.allocated': 'Allocated',
  'reporting.exceptionsTable.rejected': 'Rejected',
  'reporting.exception.noAllocations': 'No allocations',
  'reporting.exception.allRejected': 'All rejected',
  'reporting.noExceptions': 'No operational exceptions found in the selected period.',
  'reporting.operationalExceptionsError': 'Operational exceptions: {message}',
} as const;

const cs: Catalog<keyof typeof en> = {
  'reporting.error.dashboard': 'Načtení přehledu se nezdařilo.',
  'reporting.error.summary': 'Načtení souhrnu se nezdařilo.',
  'reporting.error.fairness': 'Načtení férovosti se nezdařilo.',
  'reporting.error.utilization': 'Načtení využití se nezdařilo.',
  'reporting.error.reasonCodes': 'Načtení kódů důvodů se nezdařilo.',
  'reporting.error.employeeImpact': 'Načtení dopadu na zaměstnance se nezdařilo.',
  'reporting.error.operationalExceptions': 'Načtení provozních výjimek se nezdařilo.',

  'reporting.unknownRequestor': 'Neznámý žadatel · {ref}',

  'reporting.loadingReport': 'Načítání přehledu…',
  'reporting.forbiddenTitle': 'Data přehledů nejsou pro vaši aktuální roli k dispozici.',
  'reporting.forbiddenDetail': 'K přehledům mají přístup účty HR a správce. Pokud přístup očekáváte, kontaktujte svého správce.',
  'reporting.retry': 'Zkusit znovu',

  'reporting.title': 'Přehledy parkování',
  'reporting.downloading': 'Stahování…',
  'reporting.outcomesCsv': 'CSV s výsledky přidělení',
  'reporting.summaryCsv': 'Stáhnout souhrnné CSV',

  'reporting.noDataTitle': 'Zatím žádná data přehledu',
  'reporting.noDataDetail':
    'Přehledy se generují z dokončených losování. Spusťte losování v HR operacích a zobrazí se zde výsledky přidělení, důvody zamítnutí a data o využití.',

  'reporting.stat.totalDemand': 'Celková poptávka',
  'reporting.stat.allocations': 'Přidělení',
  'reporting.stat.allocationRate': 'Míra přidělení',
  'reporting.stat.rejections': 'Zamítnutí',
  'reporting.stat.cancellations': 'Zrušení',
  'reporting.stat.noShows': 'Nedostavení se',

  'reporting.rejectionsByReason': 'Zamítnutí podle důvodu',

  'reporting.dailyTrend': 'Denní trend',
  'reporting.dailyTrendTable.date': 'Datum',
  'reporting.dailyTrendTable.demand': 'Poptávka',
  'reporting.dailyTrendTable.allocations': 'Přidělení',
  'reporting.dailyTrendTable.rate': 'Míra',

  'reporting.utilizationByLocation': 'Využití podle lokality',
  'reporting.utilizationTable.location': 'Lokalita',
  'reporting.utilizationTable.demand': 'Poptávka',
  'reporting.utilizationTable.allocated': 'Přiděleno',
  'reporting.utilizationTable.rate': 'Míra',
  'reporting.utilizationTable.rejected': 'Zamítnuto',
  'reporting.utilizationTable.cancelled': 'Zrušeno',
  'reporting.utilizationTable.noShows': 'Nedostavení se',
  'reporting.utilizationError': 'Využití: {message}',

  'reporting.reasonCodes': 'Kódy důvodů',
  'reporting.totalDemand': 'Celková poptávka: {count}',
  'reporting.reasonCodeTable.reason': 'Důvod',
  'reporting.reasonCodeTable.count': 'Počet',
  'reporting.reasonCodeTable.percentOfDemand': '% z poptávky',
  'reporting.reasonCodesError': 'Kódy důvodů: {message}',

  'reporting.dailySummary': 'Denní souhrn',
  'reporting.summaryTable.date': 'Datum',
  'reporting.summaryTable.location': 'Lokalita',
  'reporting.summaryTable.slot': 'Místo',
  'reporting.summaryTable.demand': 'Poptávka',
  'reporting.summaryTable.alloc': 'Přiděleno',
  'reporting.summaryTable.rate': 'Míra',
  'reporting.summaryTable.rejected': 'Zamítnuto',
  'reporting.summaryTable.cancelled': 'Zrušeno',
  'reporting.summaryTable.noShows': 'Nedostavení se',
  'reporting.summaryError': 'Souhrn: {message}',

  'reporting.fairness': 'Férovost',
  'reporting.fairnessTable.requestor': 'Žadatel',
  'reporting.fairnessTable.requests': 'Žádosti',
  'reporting.fairnessTable.allocations': 'Přidělení',
  'reporting.fairnessTable.rate': 'Míra',
  'reporting.fairnessError': 'Férovost: {message}',

  'reporting.employeeImpact': 'Souhrn dopadu na zaměstnance',
  'reporting.employeeImpactSubtitle':
    'Zaměstnanci s {threshold}+ zamítnutími ve zvoleném období (pseudonymizováno kvůli soukromí)',
  'reporting.employeeImpactTable.requestor': 'Žadatel',
  'reporting.employeeImpactTable.totalRequests': 'Celkem žádostí',
  'reporting.employeeImpactTable.rejections': 'Zamítnutí',
  'reporting.employeeImpactTable.allocations': 'Přidělení',
  'reporting.employeeImpactError': 'Dopad na zaměstnance: {message}',

  'reporting.operationalExceptions': 'Provozní výjimky',
  'reporting.operationalExceptionsSubtitle': 'Data se selháními losování, chybějícími přiděleními nebo zcela zamítnutou poptávkou.',
  'reporting.exceptionsTable.date': 'Datum',
  'reporting.exceptionsTable.location': 'Lokalita',
  'reporting.exceptionsTable.issue': 'Zjištění',
  'reporting.exceptionsTable.demand': 'Poptávka',
  'reporting.exceptionsTable.allocated': 'Přiděleno',
  'reporting.exceptionsTable.rejected': 'Zamítnuto',
  'reporting.exception.noAllocations': 'Žádná přidělení',
  'reporting.exception.allRejected': 'Vše zamítnuto',
  'reporting.noExceptions': 'Ve zvoleném období nebyly nalezeny žádné provozní výjimky.',
  'reporting.operationalExceptionsError': 'Provozní výjimky: {message}',
};

export const reportingMessages = { en, cs };
