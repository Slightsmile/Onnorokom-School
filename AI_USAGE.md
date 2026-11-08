# AI Usage Disclosure

This project was built with Claude (Anthropic), via Claude Code, as a pair
programmer for essentially the whole implementation: backend API and
concurrency logic, frontend UI, Dockerfiles/compose setup, tests, and these
docs. The human author wrote the implementation plan first and chose the
overall architecture, then had the AI carry out the work against that plan
while supervising the result: reviewing the architecture decisions, checking
generated code and diffs, and verifying the running system end-to-end (API
calls via curl, container restarts, test runs) before accepting it.

## What AI generated

- All backend code (`backend/src/`): the minimal API endpoints, the
  `AdmissionService` business logic (seat-hold/confirm/cancel, lazy expiry,
  row-locking for concurrency), the Postgres schema (`init.sql`), and the
  Dockerfile.
- All backend tests (`backend/tests/`), including the concurrency test.
- All frontend code (`frontend/src/`): the three screens, the API client,
  styling, and the Dockerfile.
- `docker-compose.yml` and both `.dockerignore` files.
- This set of docs (`README.md`, `ASSUMPTIONS.md`, this file).

## Where AI got it wrong, and how it was caught

- **Dapper nullable-struct bug.** The first implementation of
  `GetHoldAsync` and `ConfirmHoldAsync` queried into `Dapper`'s
  `QuerySingleOrDefaultAsync<HoldRow?>`, where `HoldRow` is a custom
  multi-field `struct`. This compiled fine, but at runtime it silently
  returned `null` even when a matching row existed in the database — Dapper's
  special-cased handling of `Nullable<T>` works for single-column scalar
  types (like `int?`), not for a `Nullable<struct-with-several-fields>`. The
  practical symptom: `GET /holds/{id}` and `POST /holds/{id}/confirm` always
  returned `404 not_found`, even right after successfully creating the hold.
  This was caught by manually exercising the full API lifecycle with curl
  against the running Docker containers (not by a unit test, since the unit
  tests exercised the *service* layer in a way that happened to still hit the
  same bug — the bug was only visible once real HTTP requests were made end
  to end). The fix: switch those two queries to `QueryAsync<HoldRow>`
  (non-nullable struct) and check `row.HoldId is null` (Dapper leaves
  unmapped string fields as `null` on a default struct) instead of relying on
  `QuerySingleOrDefaultAsync` with a nullable struct wrapper.
- **Minor doc/command drift.** An early draft of `README.md` described
  running backend tests via a `Dockerfile.test` file that was never actually
  committed to the repo (it was created as a throwaway file during manual
  verification and deleted afterward). This was caught before finalizing the
  docs, and the README was corrected to document only the command that was
  actually run and verified to work (a one-off `dotnet test` inside a
  throwaway SDK container on the compose network).

## What was NOT independently re-derived by a human

The human did not hand-write or line-by-line re-derive the SQL transaction
logic, the Dapper query code, or the React component code; they relied on
running the system (API calls, container restarts, automated tests, a visual
check of the frontend) as the main form of verification, plus a review of
the overall architecture and the final diffs.
