# Approach

How each task was tackled, in the order it was done, and what I was weighing at each step. The
README states the decisions; this is the reasoning that led to them.

## Before starting

I read the brief end to end, then the codebase before changing anything. Things that shaped the work:

- The backend is a clean architecture (Domain, Application, Infrastructure, Presentation) with
  architecture tests that fail the build if a layer reaches outwards - so new code had to fit those
  rules, not work around them.
- Reads are a separate `ITicketQueries` side that projects straight to DTOs; writes go through
  aggregates and repositories. I kept to that split.
- The seeder already carried an `SlaWindowMinutes` per ticket that nothing stored - a strong hint about
  Task 2.
- The repo README differs from the PDF (Task 4 optional, `APPROACH.md` required); I treated the repo
  copy as the latest and did both.

I got a green baseline first (`npm test`, the API, the web app) so any later failure was mine.

## Task 1 - defect and filtering

**Reproduce first.** In the running app, with the browser's network log open: picking "In progress"
sent no request; typing a search then sent `status=InProgress` with it. That matched the report exactly,
so the next question was *why*, not *whether*.

**Root cause, not symptom.** `useTicketList`'s effect had a hand-picked dependency list and a lint
suppression justified by "filters identity changes every render". That claim is false for state, and the
hand-picked list is the bug. Options I weighed:

- Add the missing fields to the list - fixes today, breaks again with the next filter (the brief warns the
  SLA filter must work "without special wiring").
- Debounce the whole filters object - one dependency, but every dropdown waits 300 ms.
- **Depend on the filters object, and debounce only the search text in its own state** - chosen. Each
  change of an applied filter is a new object, so it fetches; typing settles before it becomes a filter.

I wrote the regression tests first and watched 9 of them fail on the original code, including one that
drives the real Status dropdown and asserts the URL `fetch` received. Making requests immediate exposed
a second, latent problem - a slow earlier response could overwrite a newer one - so `useAsyncData` now
ignores stale responses (separate commit, own test).

**Filtering.** The question was where filters live so that "add one more" stays one line. A single
`ApplyFilters` with a `WhereIf` per filter on the `IQueryable`, applied before `CountAsync`/`Skip`/`Take`,
keeps everything in SQL and the count honest. Trimming the search belongs to the query contract
(`TicketQuery.Normalized`). I tested on SQLite in memory rather than mocking, so a filter EF cannot
translate fails the test, and read the generated SQL on SQL Server to confirm the `WHERE` precedes
counting and paging.

## Task 2 - triage, escalation, SLA

The brief says the rules are simple and the challenge is *where they are placed*, so I designed the
placement before writing code:

- **Pure rules in the Domain**, one per concern: `PriorityPolicy` (BR-1), `SlaPolicy` (BR-2/3/6),
  `AgentSelector` (BR-4/5), `SlaEvaluator` (BR-8). Create and escalate call the same ones.
- **Data access stays at the edges**: handlers load the customer, category and agent candidates (one
  query with open-ticket counts), then call the rules.
- **The aggregate guards its own invariants**: `Ticket.Escalate` refuses Critical/Resolved/Closed (409),
  raises one level, restarts the window and records history. It takes `SlaPolicy` so the new window can
  never mismatch the new priority.

**The hard part was BR-8.** The status must be derived, filterable and paged in SQL, and testable at exact
boundaries with a fake clock. Writing it twice (C# and a SQL predicate) invites drift. Before committing to
one expression tree used everywhere, I spiked it: a throwaway test printed the SQL that EF generated for
SQL Server and SQLite. Both translated the whole rule into one `CASE` with "now" as a parameter, so the
design was safe. Small composition helpers then plug that expression into the list, detail, filter and
(later) report queries. "At risk" needs the window length, so tickets now store `SlaWindowMinutes`; I
rejected storing an at-risk timestamp because it would freeze a threshold the brief says is configurable.

**Interpretation calls** I wrote down rather than guessed silently: an owner at their limit keeps an
escalated ticket (it already counts towards them); ties go to the lowest agent id; due-exactly-now is at
risk; the 1 h floor applies to every window.

**Frontend.** One `SlaIndicator` driven by a `Record<SlaStatus, …>` so a new status is a type error until
designed; each state has a label and a symbol, not just a colour. The escalation panel validates exactly
what the API validates, hides itself with an explanation when the rules would refuse, still shows a 409
when the server refuses anyway, and updates the ticket and history from the response without reloading.

## Task 3 - authentication

I started from what had to be true for a reviewer probing it: every endpoint closed by default, failures
indistinguishable, nothing secret in the repo.

- **Central authorization** through a fallback policy, so a forgotten attribute fails closed. A test pins
  the list of anonymous endpoints, and I checked the tests catch a regression by removing the policy (three
  failed).
- **Identical failures**: unknown email, wrong password, inactive agent and no password share one
  message; unknown emails are verified against a decoy hash so timing matches too.
- **Credentials on `Agent`**, hashed with Identity's PBKDF2 hasher behind a small interface so the
  Domain stays free of frameworks.
- **Configuration**: the signing key is validated at start-up (length, presence); only a labelled local
  placeholder is committed.
- **The actor comes from the token** via `ICurrentUser`; an API test posts a fake `escalatedBy` to prove it
  is ignored.

To test the real pipeline I added `SupportDesk.ApiTests`, hosting the API in memory over SQLite, so it
needs no database server. On the web side, a tiny session store read with `useSyncExternalStore` means a
401 anywhere clears the session and `RequireAuth` redirects on its own - no separate redirect wiring.

## Task 4 - Docker

The brief's verification list drove the design: layer the Dockerfile so restore is cached (copy only the
files that decide the package graph first); keep the image free of any environment's settings
(`.dockerignore` drops `appsettings.Development.json`); run as the image's non-root user; let compose wait
for a healthy SQL Server. Startup migration became an explicit setting rather than "is Development", so the
container can migrate an empty volume while running as Production. I then ran every verification and wrote
down the results rather than assuming them. Along the way NuGet audit flagged a vulnerable transitive
package from the SQL client; I pinned the first clean version in its own commit.

## 2.5 - SLA summary

One aggregate query, reusing the same SLA expression so the report agrees with the list. Its average
resolution uses SQL Server's `DATEDIFF`, which SQLite lacks, so instead of faking it I made those tests run
against a real SQL Server when one is configured (each makes and drops its own database) and skip otherwise.
The derived figures (rates, hours) are plain arithmetic and are unit tested without a database.

## Working method throughout

Small commits per requirement group, each building and passing on its own; tests seen failing before a fix;
every claim in the README checked against the running system (browser network log, EF's SQL log, `curl`,
Docker) rather than inferred from the code.
