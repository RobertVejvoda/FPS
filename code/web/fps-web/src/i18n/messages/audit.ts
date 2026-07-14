// LOC001 (#744) — audit console and auditor workspace copy.
const en = {} as const;

const cs: Record<keyof typeof en, string> = {};

export const auditMessages = { en, cs };
