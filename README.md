# School Admission System

First-come-first-served school admission: a seat is **held** for a student for a limited time, and becomes
**confirmed** when the fee is paid (simulated). Backend: .NET 8 + PostgreSQL. Frontend: React 18 + Vite. Everything runs with Docker.

## Run (only Docker is needed)

```bash
git clone <your-repo-url>
cd school-admission-system
docker compose up --build
```

| What      | URL                          |
|-----------|------------------------------|
| Frontend  | http://localhost:5173        |
| API       | http://localhost:8080        |
| Health    | http://localhost:8080/health/ready |

Stop with `docker compose down` and start again with `docker compose up` - data is kept in the `pgdata` volume.
(Do **not** use `docker compose down -v`, it deletes the data.)

### Configuration (environment variables)

| Variable | Default | Purpose |
|---|---|---|
| `HOLD_DURATION_SECONDS` | `600` | How long a seat is held |
| `ConnectionStrings__Default` | set in compose | PostgreSQL connection string of the API |
| `API_BASE_URL` | `http://localhost:8080` | Backend URL used by the frontend (read when the frontend container starts) |

Copy `.env.example` to `.env` to change values for Compose. Example: `HOLD_DURATION_SECONDS=30 docker compose up --build`.

### Bonus: two API processes on one database

```bash
docker compose --profile bonus up --build   # API #1 on :8080, API #2 on :8081
```

## Tests

Backend (needs Docker running, because tests start a real PostgreSQL with Testcontainers; .NET 8 SDK or newer):

```bash
cd backend
dotnet test tests/SchoolAdmission.Tests
```

Included: create class, capacity (1 seat / 2 students), **concurrency (100 requests, 10 seats -> exactly 10 holds)**,
two API instances on one DB, idempotent confirm, ownership, duplicate application, expiry without any API call, input validation (4xx, never 500).

Frontend:

```bash
cd frontend
npm ci
npm test
```

## Local development (optional)

Frontend only: `cd frontend && npm install && npm run dev` (uses `http://localhost:8080` or `VITE_API_BASE_URL`).
For the API, the simplest way is `docker compose up --build backend` and re-build after changes.

## API

`POST /classes`, `GET /classes`, `GET /classes/{id}`, `POST /classes/{id}/holds`, `GET /holds/{id}`,
`POST /holds/{id}/confirm`, `DELETE /holds/{id}`, `GET /me/holds`. Student identity is the `X-Student-Id` header (17 digits).
Errors are JSON: `{ "status": 409, "code": "SEATS_FULL", "detail": "..." }`.
See ASSUMPTIONS.md for design decisions.
