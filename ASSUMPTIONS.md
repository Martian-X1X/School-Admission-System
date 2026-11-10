# Assumptions

1. **One hold = one seat; a seat is "taken" while HELD (and not expired) or CONFIRMED.**
   `available = total - held - confirmed`. Counts are computed from the hold rows, never from a manually maintained counter, so they cannot drift.
2. **Why held + confirmed never exceeds total seats (also with 2 API processes).** Creating a hold runs in one PostgreSQL transaction that first
   locks the class row (`SELECT ... FOR UPDATE`), then counts occupied seats and inserts. Requests for the same class queue on that row lock inside the
   database, so it does not matter how many API processes exist. A class has its own lock, so different classes do not block each other.
3. **Expiry uses the database clock** (`clock_timestamp()`), not the API server clock. Expired HELD rows are ignored by every seat count and every read
   (status is reported as `EXPIRED`), so a seat reopens at the exact moment of expiry. A background worker only tidies the stored status; nothing depends on it.
4. **One active application per student per class** is enforced by a partial unique index (`status IN ('HELD','CONFIRMED')`). After CANCELLED or EXPIRED the student may apply again.
   A student may hold seats in *different* classes at the same time (the rule only says per class).
5. **Confirm** is idempotent (repeat -> `200`, counted once). Confirm after expiry -> `409 HOLD_EXPIRED`; for someone else's hold -> `403`.
   **Cancel** works only on a pending (HELD) hold -> `204`; repeating it is `204`; cancelling a CONFIRMED hold -> `409` (a paid admission is not undone by this endpoint).
6. **No login**, as the brief says. The 17-digit `X-Student-Id` is trusted as identity, and the "create class" screen/API is open. In production both would need authentication and an admin role.
   `GET /holds/{id}` needs no header (the brief lists none) and does not expose the student id.
7. IDs are UUIDs. Class name: 1-200 chars. Seats: 1-100000. Timestamps are UTC ISO-8601.
8. **Schema** is created at start-up from an idempotent SQL script guarded by a PostgreSQL advisory lock (safe when two processes start together) instead of EF migrations - fewer moving parts for a two-table schema.
9. **CORS** allows any origin so the frontend can be served from any host/port.
10. **UI** polls every 3 s; countdowns tick every second using the server's clock (Date header) so a wrong laptop clock does not matter. Server answers always win.
11. Project structure is one API project (folders for models/service/worker) rather than 4-layer Clean Architecture: the domain is two tables, so extra layers would add files without adding safety.
