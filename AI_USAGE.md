# AI Usage

This project was built with AI assistance, and this file describes that honestly.

## Tool used

- **opencode** (an interactive CLI coding agent, powered by Anthropic Claude models), used as a pair programmer across multiple sessions. Chat-level discussions (architecture trade-offs, PostgreSQL concurrency, requirement interpretation) plus direct code generation in the repository.

## What AI did

- Interpreted the assignment brief and turned it into a design: first-come-first-served holds, one transaction per seat decision, DB-clock-based expiry.
- Wrote the first draft of essentially all code:
  - Backend: minimal-API service, Dapper + raw SQL instead of EF Core, startup schema creation with an advisory lock, `FOR UPDATE` row locking for capacity checks, partial unique index as a concurrency safety net, background expiry sweeper.
  - Frontend: React 18 + Vite app (components, polling hooks, server-corrected countdown, runtime API URL config).
  - Infrastructure: Dockerfiles, docker-compose.yml (db, backend, optional second API process, frontend).
  - Tests: backend integration tests with Testcontainers (including the 100-request concurrency test), frontend Vitest component tests.
  - Docs: README.md, ASSUMPTIONS.md, and this file's structure.
- Later revision: made the UI text bilingual (English with Bengali in parentheses, e.g. "Pay Admission Fee (ভর্তি ফি দিন)") to match the assignment's Bengali button labels, and updated the affected tests.

## What I did myself

- Reviewed every generated file, read the SQL transactions and checked them against the assignment rules.
- Directed design decisions where the brief was ambiguous (idempotent confirm, cancelling a confirmed admission returning 409, `GET /holds/{id}` needing no header, UUID ids).
- [Add anything you personally changed, rejected, or fixed by hand — reviewers expect at least a few honest items here.]

## Where the AI was wrong / what I corrected

- [Fill in with real incidents from your sessions, e.g.: a suggestion that had to be reworked, a Docker or SQL detail that failed when first run, or a requirement the AI initially misread and you corrected.]

## How everything was verified

- `dotnet build` — backend compiles with no warnings.
- `cd backend && dotnet test tests/SchoolAdmission.Tests` — 8 integration tests against a real PostgreSQL (Testcontainers): class creation, 1-seat capacity, **100 concurrent requests for 10 seats → exactly 10 holds**, two API processes on one database, idempotent confirm, ownership, duplicate application, expiry without any API call, and bad-input handling (4xx, never 500).
- `cd frontend && npm ci && npm test` — 7 Vitest tests (countdown, disabled buttons after expiry, student-ID validation, error-message mapping).
- Manual: `docker compose up --build`, then creating a class, applying, confirming, cancelling, and watching a hold expire through the UI. [Confirm you actually did this walkthrough before submitting.]
