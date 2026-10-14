# Northwind Support Desk

A support ticket system: raise, filter, triage, escalate and resolve tickets.
**.NET 10 Web API** (clean architecture, EF Core) + **React 19 / TypeScript** + **SQL Server**.

This fork completes the candidate assignment ([`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)).
All four tasks and the optional SLA summary report are done; [`APPROACH.md`](APPROACH.md) has the
thinking behind each one.

| # | Task | Status |
| --- | --- | --- |
| 1 | Defect fix + server-side filtering | Done |
| 2 | Triage, escalation, SLA status (+ 2.5 SLA summary report) | Done |
| 3 | Login and authentication | Done |
| 4 | Dockerise the API | Done |

---

## Run it

**Prerequisites:** .NET SDK 10, Node.js 22 (20.19+ works), Docker.

```bash
npm install            # the web app
npm run db:up          # SQL Server 2022 in Docker on localhost:1433
npm run api            # terminal 1: migrates, seeds, serves https://localhost:7043 (http :5043)
npm run web            # terminal 2: http://localhost:5173
```

Or the API in a container, against the same compose SQL Server:

```bash
docker compose up --build                                   # API on http://localhost:5080
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web     # web app against the container
```

(PowerShell: `$env:VITE_API_PROXY_TARGET="http://localhost:5080"; npm run web`.)

### Development login

Every seeded agent signs in with their email and the password **`LocalDev-Only-1`** - a local
fixture (`SupportDbSeeder.DevelopmentPassword`), hashed at seed time; it exists in no other database.

| Agent | Email | |
| --- | --- | --- |
| Alex Turner | `alex.turner@northwind-support.example` | General, Technical |
| Priya Nair | `priya.nair@northwind-support.example` | Outage, Security |
| Sara Lindqvist | `sara.lindqvist@northwind-support.example` | General, Billing |
| Ben Osei, Marcus Doyle | `ben.osei@…`, `marcus.doyle@…` (same domain) | |
| Yuki Tanaka | `yuki.tanaka@…` | inactive: refused, by design |

A database seeded before this work has agents without passwords: `npm run db:reset`, then restart the API.
Swagger: sign in with `POST /api/auth/login`, then paste the token into **Authorize**.

## Tests

```bash
npm test               # dotnet test, then Vitest - no database needed
```

The SLA summary's aggregation uses SQL Server's `DATEDIFF`, so its three tests run only against a real
SQL Server (each creates and drops its own database) and are skipped otherwise:

```bash
SUPPORTDESK_TEST_SQLSERVER="Server=localhost,1433;User Id=sa;Password=Local_Dev_Password1;TrustServerCertificate=True;Encrypt=False" dotnet test
```

| Suite | What it covers |
| --- | --- |
| `SupportDesk.UnitTests` (122) | Domain rules at their boundaries with `FixedClock`/`TicketBuilder`; handlers with Moq; filtering, SLA filter and escalation history as real SQL on SQLite in memory; the report on SQL Server |
| `SupportDesk.ApiTests` (10, new) | The real HTTP pipeline in memory over SQLite: 401 without / 200 with a token, tampered token, identical login failures, inactive agent, a deactivated agent's live token refused, `/me`, actor from token, `/health`, and that only login and health are anonymous |
| `SupportDesk.ArchitectureTests` (4) | Layer dependencies; the EF model matches the migrations |
| Web, Vitest (44) | DEFECT-117 regression (every filter, the real dropdown); out-of-order responses; SLA indicator states; escalation validation, 409 and refresh; token attach and 401 handling; login redirect and return; the SLA report page |

**Deliberately not tested:** CSS and layout; Swagger; `SystemClock`; the seeder's data beyond what the
API tests use; the Docker image (checked by hand, below); browser-level end-to-end (checked by hand).

---

## The defect: DEFECT-117

**Root cause.** `useTicketList`'s fetch effect listed only `search`, `page` and `pageSize` as dependencies,
with the exhaustive-deps lint rule switched off by a comment claiming the filters object "changes every
render". It does not - it is state - so changing status (or any other filter) updated state but never
re-ran the fetch; the next search change then fetched with the latest state, carrying the old status along.

**Fix.** The fetch depends on the `filters` state object itself (through the existing `useAsyncData`),
whose identity changes exactly when a filter does. Only the search text is debounced: it lives in its own
state and reaches `filters` after 300 ms. Dropdowns fire at once, typing sends one request, and any new
filter (the SLA filter) needed no wiring. The lint suppression is gone. Separately, `useAsyncData` now
ignores out-of-order responses, which immediate requests made more likely.

**Verify.** `useTicketList.test.ts` and `TicketListPage.test.tsx` fail on the original code (9 tests) and
pass now; in the browser, the network tab shows one `GET /api/tickets` per filter change.

## Filtering

All filters are applied in `TicketQueries.ApplyFilters` as one `WhereIf` per filter on the `IQueryable`,
before `CountAsync`, `Skip` and `Take`, so SQL does the filtering, counting and paging and `totalCount`
describes the filtered set. Search is a parameterised, escaped `LIKE` over title, reference and customer
name; `TicketQuery.Normalized()` trims it and drops it when blank. **Checked** with EF's command log on
SQL Server (the `WHERE` appears in both the `COUNT(*)` and the paged query, before `OFFSET`) and by tests
that run the real query on SQLite, where anything untranslatable fails. **The next filter** is one more
`WhereIf` line - which is exactly how `slaStatus` was added.

## Design decisions

| Decision | Rejected | Why |
| --- | --- | --- |
| Triage rules as small pure classes in the Domain (`PriorityPolicy`, `SlaPolicy`, `AgentSelector`, `SlaEvaluator`) shared by create and escalate | Rules in handlers or controllers; interfaces around each rule | Testable without mocks, impossible to drift between create and escalate. Interfaces only at real seams: clock, repositories, read queries, hasher, token issuer, current user |
| SLA status written **once**, as an expression tree; SQL runs it for list, detail, filter and report; tests compile the same tree | A C# evaluator plus a hand-written SQL predicate | Two encodings of BR-8 would drift; this way the filter cannot disagree with the badge. `ExpressionComposition` (15 lines) plugs it into queries |
| Store `SlaWindowMinutes` on the ticket | Store an "at-risk-from" timestamp; derive the window from escalation history | "At risk" is a share of the window, so its length must be known. A stored timestamp would freeze today's threshold (BR-6 says it is configurable); history would need a subquery per row. Migration backfills existing rows |
| `Sla` settings moved to `appsettings.json`, validated at start-up | Leave them in `appsettings.Development.json` | They are business rules for every environment - the container runs as Production and has no Development file |
| Escalation: an owner at their limit **keeps** the ticket | Reassign whenever the owner is at the limit | The limit guards new work; the ticket already counts towards them. Checked as `open - 1 < max` |
| Tie-break: lowest agent id | Name; random | Stable and meaningless to game |
| Credentials as a column on `Agent` | Separate `Users` table | Every user is an agent with one login; no other user kinds or login methods exist to model |
| Token in `sessionStorage` | `localStorage`; httpOnly cookie | Survives reload, not tab close, never sent implicitly. A cookie resists XSS better but needs CSRF protection and a different API contract - noted below |
| Startup migration behind `Database:MigrateAndSeedOnStartup` | "If Development" | The container must migrate an empty volume without running as Development |
| `/health` and login are the only `[AllowAnonymous]` | Opening Swagger/health per controller | A fallback policy closes every endpoint by default; a test pins the open list |

**Assumptions** (spec open to interpretation): all filters AND, so `unassignedOnly` with an agent filter
returns nothing; search ignores case through the database collation; the 1 h floor applies to every window;
"due exactly now" is at risk, not breached; at-risk margin is whole minutes (SQL and C# agree);
`escalatedBy` stores the agent's display name; inactive agents get the same login failure as a wrong password.

**Patterns.** *Specification-style composition* (`WhereIf` and composed expressions) keeps filters and the
SLA rule declarative and translatable. *Domain service / double dispatch* - `Ticket.Escalate(SlaPolicy, …)` -
lets the aggregate guard its invariants while the window always matches the new priority. *Options pattern
with fail-fast validation* (`SlaSettings`, `JwtOptions`) makes bad configuration a start-up error, not a
wrong due date.

## Database

Three migrations: `AddTicketSlaWindow` (`Tickets.SlaWindowMinutes`, check `> 0`, backfill),
`AddTicketEscalations`, `AddAgentPasswordHash` (`nvarchar(256)`, nullable). `TicketEscalations` has FKs to
the ticket and to both agents, all **Restrict** - it is an audit trail, never deleted as a side effect;
lengths 500/100; check constraints that priorities are valid and rise. **Index:**
`(TicketId, EscalatedAtUtc DESC)` - the only read is "this ticket's history, newest first", which becomes a
seek with no sort, and leading with `TicketId` also serves the foreign key (EF adds no duplicate). Reads use
`AsNoTracking` and project to DTOs; agent workloads and history names come from one query each (no N+1).
`db/*.sql` describe the starter schema only; the migrations are the source of truth.

## Authentication

Passwords: ASP.NET Core Identity's `PasswordHasher` (PBKDF2-HMAC-SHA512, per-password salt, 100k
iterations, parameters in the hash). Unknown emails are verified against a decoy hash so timing does not
reveal accounts. `POST /api/auth/login` issues an HS256 JWT (`sub`, `name`, `email`), valid **60 minutes** -
long enough for a working session, short enough to limit a stolen token, since there are no refresh tokens.
The key comes from `Jwt:SigningKey` / `Jwt__SigningKey` (≥ 32 bytes, checked at start-up); only a labelled
local placeholder is committed. Every authenticated request also re-checks that the token's agent is still
active (one primary-key lookup), so deactivating an agent takes effect on their next request, not at expiry. The web
app attaches the token in `api/client.ts`; any 401 ends the session and `RequireAuth` returns the agent to
`/login`, then to the page they wanted. **For production:** keys from a secret store with rotation, an
httpOnly cookie or shorter tokens plus refresh, per-token revocation (a security stamp), lockout and rate
limiting on login, HTTPS everywhere, and audit logging of sign-ins.

## Docker

`apps/api/Dockerfile` (build from the repo root): SDK stage restores from `global.json`,
`Directory.*.props` and the `.csproj` files before copying source; runtime stage is `aspnet:10.0`, HTTP
8080, user `app`. `.dockerignore` also excludes `appsettings.Development.json`, so no development
connection string or key reaches the image. Run-time variables (see `.env.example`):
`ASPNETCORE_ENVIRONMENT` (default Production), `ConnectionStrings__Default` (compose builds it for host
`sqlserver`), `Jwt__SigningKey`, `Database__MigrateAndSeedOnStartup`.

| Verification | Result |
| --- | --- |
| `docker compose down -v && docker compose up --build` | API waits for a healthy SQL Server, applies 4 migrations, seeds; `/health` → `{"status":"Healthy","checks":{"database":"Healthy"}}`; `/api/tickets` with a token → 40 tickets |
| Change one `.cs` file, rebuild | every step through `dotnet restore` reported `CACHED`; only source copy and publish re-ran |
| Image size | runtime **455 MB** vs SDK **1.3 GB** on disk (125 MB vs 351 MB compressed; the aspnet base is 340 MB) |
| `docker compose exec api whoami` | `app` (uid 1654) |
| Web against the container | login and the ticket list through Vite's proxy to `:5080` |

**Production migrations** would not run at application start-up: several replicas would race, the app's
database user would need DDL rights, and a failed migration would crash-loop the service. Instead the
pipeline runs an EF migration bundle (or a reviewed idempotent script) as its own step before deploying,
with an identity that may alter the schema. **Before production I would also** pin base images by digest,
publish for one runtime (drops ~30 MB of other platforms' native files, plus SqlClient's unused 37 MB
`libmsalruntime`) or use a chiseled image, persist DataProtection keys (logged warning; JWTs do not use
them), and add a container `HEALTHCHECK`.

## SLA summary report (2.5)

`GET /api/reports/sla-summary?from=&to=&groupBy=agent|priority` - per group: total, breached, at risk,
within, met, no-SLA, breach rate (over tickets that have an SLA) and average resolution hours, for tickets
created in `[from, to)`. One SQL statement (`GROUP BY` with conditional counts); the SLA status inside it is
the same composed rule. In the web app: **SLA report** in the header (`/reports/sla`) - group by agent or
priority, an optional date range of whole UTC days (the end day included), and the table updates as the
controls change; a reversed range is refused before any request.

## AI usage

**Tools.** Claude Code (Claude Opus) in the terminal and desktop app, as a pair programmer for the whole
assignment: reading the brief and codebase, proposing designs, writing code and tests, running builds,
tests, Docker and a browser against the running app. I set the order of work, made the design calls listed
above and reviewed each change before it was committed.

**A workflow that worked.** Reproduce before fixing (the defect was confirmed in the browser's network
log first), then write tests that fail on the old code, then fix; and spike the risky bit before designing
around it - a throwaway test proved EF could translate the SLA rule to SQL on both SQL Server and SQLite
before the design depended on it.

**Accepted as-is.** The `WhereIf` filter shape and the JWT bearer configuration were accepted largely as
proposed after reading them through.

**Changed or rejected.** Rejected debouncing *all* filters (simpler, but delays every dropdown by 300 ms);
rejected storing an at-risk timestamp (would freeze the configurable threshold); rejected a `Users` table;
changed `ToDueAtUtc` from nullable to required once it was clear escalation always sets it; split commits
so the defect fix, the filtering and each requirement group stand alone.

**Bugs.** Introduced by the AI and caught: the first `useSession` read the session through a function that
could clear it - a side effect during render (fixed with a pure read); a report test treated
`default(DateTime?)` as "use the default range", hiding the no-range case (caught when the test ran on
SQL Server); a dependency commit accidentally included an unrelated package line (redone). Found with
the AI's help: the defect's root cause in the suppressed lint rule; out-of-order responses;
a vulnerable transitive package (`System.Security.Cryptography.Pkcs`, pinned to 10.0.13).

**Verification.** Every test was seen failing before the fix where that made sense; protection tests were
checked by removing the fallback policy (three tests failed); SQL was read in EF's logs on SQL Server;
status codes were checked with `curl`; the UI was exercised in a browser; the container checks above were
run, not assumed.

## Known limitations

- No locking around assignment: two simultaneous creates can both pick an agent with one slot left.
- A stolen token stays valid until it expires (at most 60 min); deactivation is honoured at once, but
  revoking one token of an active agent needs a security stamp or token version on the agent.
- The token in `sessionStorage` is readable by script on the page (XSS). React escapes rendered text and
  nothing injects raw HTML; a Content Security Policy, or an httpOnly cookie with CSRF protection, would harden it.
- Out of scope, not built: registration, password reset, refresh tokens, MFA, lockout and rate limiting,
  roles, OAuth/SSO, HTTPS in the container, CI/CD changes, notifications.
- Search uses `LIKE '%…%'` and the SLA filter a computed `CASE`; neither can use an index. At 10 million
  tickets: full-text search, and either a persisted at-risk column refreshed on threshold changes or
  pre-aggregated reporting.
- `NextReferenceAsync` assumes a single API instance (a database sequence would fix it).

## Time spent

Roughly 11 hours: setup and reading 1 h; Task 1 1.5 h; Task 2 4 h; Task 3 2 h; Task 4 1 h;
report 0.5 h; README and APPROACH 1 h.
