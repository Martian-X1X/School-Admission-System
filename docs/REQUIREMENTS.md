# Requirements

School Admission System — a seat-reservation service for school admissions, following
**First Come, First Served (FCFS)**. It is not a lottery system.

Two user groups:

| User | Responsibility |
|---|---|
| School Administrator | Manage admission classes and their seat capacities |
| Parent / Guardian | Apply with a student birth registration number, view classes, reserve a seat, confirm admission or cancel an application |

## Functional requirements

1. Create admission classes (name + total seats).
2. List classes with seat statistics (total, held, confirmed, available).
3. View individual class details.
4. Apply for admission (create a seat hold) with a 17-digit student birth registration number.
5. Hold a seat temporarily for a configurable duration (default 600 seconds).
6. Confirm an admission (completing an existing hold).
7. Cancel a reservation.
8. Track a student's applications (current and previous statuses).
9. Automatically release expired seats back into the available pool.
10. The class list and seat counts update in the UI automatically (no manual reload).
11. A held application visibly transitions to `EXPIRED` in the UI without a page reload.

## Technical requirements

1. Backend: ASP.NET Core (.NET 8 or newer) — this project uses .NET 9.
2. Database: PostgreSQL (EF Core + Npgsql).
3. Frontend: React 18+ (Vite, JavaScript).
4. Containerized with Docker and Docker Compose.
5. REST API served at `http://localhost:8080`.
6. Hold duration configurable via `HOLD_DURATION_SECONDS` (default 600).
7. Data must survive Docker restarts (persistent volume for PostgreSQL).
8. Seat availability invariant `Held + Confirmed ≤ TotalSeats` must hold under any number of simultaneous requests.

## Non-functional requirements

1. Concurrent request safety (row locking + database constraints).
2. Data persistence across container restarts.
3. Maintainable architecture (Clean Architecture, modular monolith).
4. Proper error handling (Problem Details, no stack traces leaked to clients).
5. Configurable application settings with startup validation.
6. Automatic UI updates (polling + countdown).

## Submission requirements

1. Public GitHub repository.
2. `docker-compose.yml` (one-command startup).
3. `README.md` with setup/run instructions.
4. `ASSUMPTIONS.md` documenting scope decisions.
5. `AI_USAGE.md` documenting AI assistance.
6. At least three backend automated tests, **including a concurrency test**.

## Seat arithmetic

```
Available = Total − Held − Confirmed
```

`Held` counts only **non-expired** `HELD` applications. `Confirmed` counts `CONFIRMED` applications. `EXPIRED` and `CANCELLED` applications release their seats.
