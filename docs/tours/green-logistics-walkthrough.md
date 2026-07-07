# Green Logistics Walkthrough

A guided, end-to-end run through the **Green Logistics** showcase — the small, synthetic tenant built for evaluation. It walks the full loop: sign in → request → obligations honoured → Draw → notify → act → evidence. Use synthetic demo data only unless a customer-approved pilot explicitly changes that rule.

## Before you start

Bring the container stack up with seed data:

```bash
./tools/start-container-stack.sh --seed
```

| What | Where |
| --- | --- |
| API gateway | `http://localhost:10000` |
| Web app | `./tools/start-smoke-web.sh` → `http://localhost:5200` |
| Mobile (Expo) | `./tools/start-smoke-mobile.sh` |
| Keycloak sign-in | `http://localhost:8180` (realm `fps-local`) |

All Green Logistics demo users share the password `Dev1234!` and live in tenant `greenlogistics` (work-email domain `greenlogistics.example`, which also demonstrates company-SSO tenant discovery). The full roster, locations, and slots are in [Demo Seed Data](./demo-seed-data).

**Cast for this walkthrough:**

| User | Role | Part they play |
| --- | --- | --- |
| `gl-employee5` (Pavel Cerny) | employee | General participant in the fair Draw |
| `gl-employee1` (Jan Novak) | employee | Company-car holder — fixed slot `VIP-01` (Tier-1) |
| `gl-hr-admin` (Lucie Prochazkova) | employee, hr_manager | Policy, locations/slots, reports |
| `gl-tenant-admin` (Karel Urban) | admin | Runs the Demo Draw; readiness |
| `gl-auditor` (Martin Cerny) | auditor | Reviews audit evidence |

## The script

| Step | User | Do this | Expect to see | If a screen isn't available |
| --- | --- | --- | --- | --- |
| 1 | `gl-employee5` | Sign in (mobile or web); open the shell. | Tenant/user/roles resolved from sign-in — you never type a tenant or user id (`GET /me` resolves context). | Call `GET /me` through the gateway and show the resolved tenant/user. |
| 2 | `gl-employee5` | Submit a `GL-HQ` parking request for the seeded date. | The request appears with a safe status and, where relevant, an employee-visible reason. | Show the booking request via the booking API response. |
| 3 | `gl-employee1` | Submit an on-time request as the company-car holder. | The fixed company-car slot `VIP-01` is allocated **before** the fair Draw; the employee cannot self-assign company-car status. | Point to the seeded `VIP-01` obligation in [Demo Seed Data](./demo-seed-data). |
| 4 | `gl-hr-admin` | Show tenant policy, the `GL-HQ` location, its six slots, and capacity. | Allocation behaviour maps to configured policy and capacity (general, EV, accessible, motorcycle, company-car). | 📷 Screenshot gap — describe the seeded slots from [Demo Seed Data](./demo-seed-data). |
| 5 | `gl-tenant-admin` | Run the admin-only Demo Draw for the seeded requests (or show the completed result). | Ten requests, demand &gt; capacity → a mix of allocated and waitlisted by documented rules; the same Draw key is idempotent; lottery internals stay out of employee views. | The seed already runs the Draw; show its verified result. |
| 6 | `gl-employee5` | View the booking result and notifications. | Notification history/unread state reflects the booking event (allocated or waitlisted with a safe reason). | 📷 Screenshot gap — show the notification API records. |
| 7 | `gl-employee5` | Cancel an allocated booking. | The next fairly-ranked waitlisted employee is promoted — a freed space still flows by the rules. | The seed demonstrates this reallocation; point to `verify_demo_draw`. |
| 8 | `gl-auditor` | Query audit records for the booking and Draw actions. | Audit uses stable/pseudonymised identifiers and avoids unnecessary PII. | Show the audit query API response. |
| 9 | `gl-hr-admin` | Open the parking summary report. | Tenant-scoped operational and fairness summary (`GET /reports/parking/summary`). | Call the reporting summary endpoint directly. |

## Fallback notes

- **No captured screenshots yet.** Where a real product screen isn't available, the steps above give an API-level fallback so the evidence is still visible. Screens are marked with 📷 and will be captured from real demo flows.
- **Draw timing.** Seeded requests are dated at least two workdays out so they clear the Draw cutoff; if you create fresh requests, respect the cutoff or the Draw won't include them.
- **Isolation check (optional).** Enable the bare `demo` tenant (`FPS_INCLUDE_DEMO_TENANT=1 ./tools/dev-setup-auth.sh`) to show that a second tenant sees none of Green Logistics' data.
- **Reset.** Re-running the seed rebuilds the showcase; demo credentials and seed/reset actions are for approved evaluators or authenticated tenant admins only — never anonymous public functionality.

## Related tours

- [Resource User Tour](./tours/resource-user) — the participant's view of steps 1–7.
- [HR &amp; Operator Tour](./tours/operator-hr) — the operator's view of policy, the Draw, and evidence.
- [Auditor &amp; Security Tour](./tours/auditor-security) — the audit and privacy view of step 8.
