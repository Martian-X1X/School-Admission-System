# Business Rules

Single source of truth for admission business logic.

## Core invariant

```
Held + Confirmed ≤ TotalSeats   (must hold under any concurrency)
Available = Total − Held − Confirmed
```

- `Held` counts `HELD` applications whose `expires_at` is in the future **at evaluation time**.
- `Confirmed` counts `CONFIRMED` applications.
- `EXPIRED` and `CANCELLED` applications occupy no seat.

## Rules

| # | Rule | Enforcement |
|---|---|---|
| B1 | A class has a positive total seat capacity | Domain constructor + API validation (400) |
| B2 | No overbooking under concurrent requests | DB transaction + `SELECT … FOR UPDATE` row lock on the class row + occupancy recount inside the transaction |
| B3 | One active or confirmed application per student per class | Partial unique index `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` + pre-check inside the transaction |
| B4 | Hold duration is configurable, default 600 s | `HOLD_DURATION_SECONDS` → `Admission:HoldDurationSeconds` (startup validation) |
| B5 | An expired seat is automatically released | (a) background worker flips `HELD → EXPIRED` in batches; (b) occupancy and confirmation logic independently compare against DB `clock_timestamp()` — the worker is never the authority |
| B6 | Only the application owner can confirm | `X-Student-Id` header compared with hold's `student_id` (403) |
| B7 | Only the application owner can cancel | `X-Student-Id` header compared with hold's `student_id` (403) |
| B8 | Repeated confirmation must not increase confirmed seats more than once | Idempotent: confirming a `CONFIRMED` hold returns the existing confirmed result; the transition only flips status once |
| B9 | Expired holds cannot be confirmed | On confirm, if DB time > `expires_at`, hold flips to `EXPIRED` and confirmation is rejected (409) |
| B10 | Cancelled / expired holds cannot be confirmed; confirmed holds cannot be cancelled | State machine below (409 on invalid transition) |
| B11 | Only `HELD` applications can be cancelled; cancelling an already-`CANCELLED` hold is idempotent (204) | State machine below |
| B12 | Expiration timestamps come from the database clock | `clock_timestamp()` via Npgsql; browser time is never authoritative |
| B13 | Student identity is the 17-digit birth registration number | `^[0-9]{17}$` validation (400) |
| B14 | Applications survive reloads / browser restarts | PostgreSQL persistence + `GET /me/holds` re-query |
| B15 | Class seat counts update automatically | Frontend polling (4 s) |

## State machine

```
            confirm                expire (worker / lazy check)
  HELD ─────────────────► CONFIRMED
   │
   ├── cancel ──► CANCELLED
   └── expiry  ──► EXPIRED
```

Allowed transitions:

| From | To | Trigger |
|---|---|---|
| HELD | CONFIRMED | Owner confirms before expiry |
| HELD | EXPIRED | `expires_at` reached (worker batch or lazy check) |
| HELD | CANCELLED | Owner cancels |

Forbidden transitions (rejected with 409):

- `EXPIRED → CONFIRMED`
- `CANCELLED → CONFIRMED`
- `CONFIRMED → CANCELLED`
- `EXPIRED → CANCELLED` (an expired hold cannot be cancelled; its seat is already released)

Idempotent operations:

- `CONFIRMED → confirm` → returns existing confirmed hold (200)
- `CANCELLED → cancel` → returns 204 again

## Hold lifecycle (create)

1. Validate 17-digit student ID.
2. Begin DB transaction.
3. Lock the admission class row (`FOR UPDATE`).
4. Flip overdue `HELD` rows in this class to `EXPIRED`.
5. Reject if student already has an active/confirmed application for this class (409 duplicate).
6. Recount occupancy from rows (non-expired HELD + CONFIRMED).
7. If occupancy ≥ total seats → 409 seat unavailable.
8. Insert `HELD` row with `expires_at = clock_timestamp() + hold duration`.
9. Commit; return 201.

## First Come, First Served

Seat allocation order is determined by which transaction acquires the class row lock
first. There is no lottery, queue or randomization.
