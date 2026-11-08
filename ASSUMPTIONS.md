# Assumptions

Things the assignment brief left open to interpretation, and what this
implementation assumes/decided, with reasoning.

## IDs

- Class IDs and hold IDs are generated server-side as `c_<10 hex chars>` and
  `h_<10 hex chars>` respectively (random, derived from a GUID). The brief
  explicitly says the example IDs (`c_123`, `h_789`) are just examples and the
  format is up to the implementer.
- The student identity is whatever string is sent in `X-Student-Id`. The UI
  enforces a 17-character input (per the brief's "17-digit registration
  number"), but the API itself does not validate the format/length of the
  header value — it only requires the header to be present and non-empty.
  This keeps the backend contract simple and avoids rejecting values the
  automated tests might use that don't look like a real registration number.

## Concurrency / seat accounting

- `heldSeats` only counts holds that are `HELD` **and not yet expired**
  (`expires_at > now()`). An expired hold is never written to the database as
  `EXPIRED` — it's a derived/computed status at read time ("lazy expiry"),
  which is how the brief's requirement ("seat frees itself automatically, no
  API call needed") is satisfied without a background job.
- Seat-limit enforcement uses `SELECT ... FOR UPDATE` on the `classes` row
  inside the transaction that creates a hold. This serializes all hold
  creation attempts for the same class through Postgres row locking, which
  is correct even if two backend processes are running against the same
  database (the bonus scenario), since the lock lives in the database, not
  in process memory.
- "One active hold per student per class" (HELD-not-expired, or CONFIRMED) is
  enforced in application code inside the same locked transaction, rather
  than via a DB unique constraint, because "not expired" can't be expressed
  in a static partial unique index without also tracking wall-clock time at
  index-maintenance time.

## Confirm semantics

- Confirming an already-`CONFIRMED` hold returns `200` with the same body
  (idempotent no-op) rather than an error, per the brief's explicit rule that
  confirming twice must not double-count the seat.
- Confirming a hold that is expired, cancelled, or doesn't belong to the
  calling student returns `409` (for expired/cancelled — "wrong state to
  confirm") or `403` (for wrong student) rather than `404`, so the client can
  distinguish "this hold doesn't exist" from "this hold exists but isn't
  yours / isn't holdable anymore."

## Errors

- All 4xx error bodies use the shape `{ "error": "<code>", "message": "<text>" }`.
  This is an additive field beyond the brief's required response shapes (the
  brief explicitly allows extra fields, just not removing/renaming required
  ones), and gives the frontend a stable machine-readable `error` code to
  branch on if needed, while `message` is what's shown to the user.
- A missing or empty `X-Student-Id` header on an endpoint that requires it
  returns `400` (bad request), not `401`, since there is no authentication in
  this system — the header is an identity label, not a credential.

## Frontend

- Polling interval is 3 seconds for both the class list and "my applications"
  view. This is frequent enough to feel "live" without reload per the brief's
  requirement, without hammering the API.
- The student's registration number is persisted in `localStorage` so that a
  page refresh or reopening the browser still shows "their" applications, per
  the brief's requirement that this survive a refresh/reopen. This is a
  convenience only — the actual identity check is always the `X-Student-Id`
  header sent with each request, never anything stored server-side per
  browser session.
- "UI doesn't need to look beautiful, just correct and easy to use" (per the
  brief) — the UI is intentionally plain: three tabs (classes, my
  applications, admin), simple tables and forms, no component library.

## Bonus

- Dual-instance support (two backend processes against one database) is
  satisfied by construction, since all seat-limit and duplicate-hold checks
  go through Postgres transactions/row locks rather than in-memory state —
  no additional code path was needed for this; it was verified manually by
  running a second backend container against the same `db` service.
- Frontend automated tests were not added in the time available; this is the
  one bonus item left undone.
