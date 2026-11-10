# Database Design

PostgreSQL, accessed via EF Core 9 + Npgsql. Schema `admission` (both tables live in the
default `public` schema; names below are physical).

## admission_classes

| Column | Type | Constraints |
|---|---|---|
| `id` | `uuid` | PK |
| `name` | `text` | NOT NULL, required |
| `total_seats` | `integer` | NOT NULL, CHECK `total_seats > 0` |
| `created_at` | `timestamptz` | NOT NULL |

## admission_holds

| Column | Type | Constraints |
|---|---|---|
| `id` | `uuid` | PK |
| `class_id` | `uuid` | NOT NULL, FK → `admission_classes(id)` |
| `student_id` | `varchar(17)` | NOT NULL, CHECK `student_id ~ '^[0-9]{17}$'` |
| `status` | `integer` | NOT NULL (`1=Held, 2=Confirmed, 3=Expired, 4=Cancelled`) |
| `created_at` | `timestamptz` | NOT NULL |
| `expires_at` | `timestamptz` | NOT NULL, CHECK `expires_at > created_at` |
| `confirmed_at` | `timestamptz` | NULL |

## Indexes

```sql
-- One active or confirmed application per student per class (DB backstop for B3)
CREATE UNIQUE INDEX ux_holds_active_student_class
    ON admission_holds (class_id, student_id)
    WHERE status IN (1, 2);  -- Held, Confirmed

CREATE INDEX ix_holds_status_expires ON admission_holds (status, expires_at);
CREATE INDEX ix_holds_student ON admission_holds (student_id);
```

The partial unique index is the authoritative guarantee of rule B3: even a bug in
application code cannot create two active applications for the same student+class —
PostgreSQL rejects the second insert.

## Concurrency strategy

Seat reservation (`CreateHold`) and confirmation run inside a single transaction:

```sql
BEGIN;
  SELECT * FROM admission_classes WHERE id = @classId FOR UPDATE;   -- row lock, serializes per class
  UPDATE admission_holds SET status = 3                             -- lazy expiry
    WHERE class_id = @classId AND status = 1 AND expires_at <= clock_timestamp();
  -- duplicate check, occupancy recount, insert --
COMMIT;
```

- `FOR UPDATE` serializes competing transactions per class; the loser blocks until the
  winner commits, then re-reads fresh counts → FCFS, no overbooking.
- Occupancy = count of `HELD` (non-expired) + `CONFIRMED` rows for the class, recomputed
  inside the locked transaction — never a manually maintained counter.
- Expiration uses DB `clock_timestamp()`, so all application servers agree on time.

## Background maintenance

```sql
UPDATE admission_holds SET status = 3   -- EXPIRED
WHERE status = 1                        -- HELD
  AND expires_at <= clock_timestamp();
```

Executed by `HoldExpirationWorker` in bounded batches (default 500 rows / 5 s). The worker
only persists the stored status; every read path re-checks `expires_at` against
`clock_timestamp()`, so correctness never depends on the worker running.

## Persistence

Docker volumes: a named volume `postgres-data` backs `/var/lib/postgresql/data`, so all
rows survive `docker compose down`/`up` and container restarts.
