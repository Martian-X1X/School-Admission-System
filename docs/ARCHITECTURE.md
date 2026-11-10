# Architecture

## Overview

A **modular monolith** using **Clean Architecture**. One deployable backend, internally
divided into logical modules (Class Management, Admission Application, Admission
Confirmation, Application Tracking). Business rules are isolated from infrastructure
(PostgreSQL, HTTP, background workers).

```
┌─────────────────────────────────────────────┐
│ Presentation — React 18 (Vite)              │
│   Admin UI (create classes)                 │
│   Parent UI (apply / confirm / cancel)       │
└───────────────────┬─────────────────────────┘
          REST API · JSON · HTTP (:8080)
┌───────────────────▼─────────────────────────┐
│ SchoolAdmission.Api — controllers, middleware│
├─────────────────────────────────────────────┤
│ SchoolAdmission.Application — use cases,     │
│   DTOs, validation, orchestration            │
├─────────────────────────────────────────────┤
│ SchoolAdmission.Domain — entities, enums,    │
│   state machine, domain exceptions           │
├─────────────────────────────────────────────┤
│ SchoolAdmission.Infrastructure — EF Core,    │
│   Npgsql, transactional SQL, background      │
│   worker, migrations                         │
└───────────────────┬─────────────────────────┘
                    │
            PostgreSQL (Docker volume)
```

## Projects

| Project | Responsibility | Dependencies |
|---|---|---|
| `SchoolAdmission.Domain` | Pure business entities, `HoldStatus`, state transitions, domain exceptions | none |
| `SchoolAdmission.Application` | Use-case services (`ClassService`, `HoldService`), DTOs, validators, repository interfaces | Domain |
| `SchoolAdmission.Infrastructure` | `AppDbContext`, entity configurations + constraints, `HoldRepository` (transactional SQL + row locks), `HoldExpirationWorker`, DI registration | Application, Domain |
| `SchoolAdmission.Api` | `ClassesController`, `HoldsController`, Problem Details exception middleware, request logging, config binding | Application, Infrastructure |
| `SchoolAdmission.UnitTests` | Domain rules, state machine, validation, expiration math | Domain, Application |
| `SchoolAdmission.IntegrationTests` | Real PostgreSQL via Testcontainers: API routes, constraints, concurrency | all |

Dependency direction: Api → Application, Infrastructure; Infrastructure → Application, Domain;
Application → Domain. No cycles. Domain depends on nothing.

## Modules inside the monolith

| Module | Location |
|---|---|
| Class Management | Application `ClassService` + Api `ClassesController` |
| Admission Application | Application `HoldService.CreateHold` + Infrastructure `HoldRepository.CreateHoldAsync` |
| Admission Confirmation | Application `HoldService.Confirm/Cancel` + transactional repo |
| Application Tracking | Application `HoldService.GetStudentHolds` + `GET /me/holds` |

## Key design decisions

1. **No MediatR / CQRS framework.** Straightforward application services per use case.
   EF Core's `DbContext` already provides unit-of-work semantics; only the seat-reservation
   transaction is abstracted behind `IHoldRepository` because it needs explicit raw-SQL
   locking that EF LINQ cannot express.
2. **Database is the concurrency authority.** `FOR UPDATE` row locks + partial unique
   index + CHECK constraints. The frontend is never trusted for seat decisions.
3. **Expiry checked everywhere.** The background worker persists status, but occupancy
   and confirmation logic independently compare `expires_at` with `clock_timestamp()`.
4. **Idempotent transitions.** Confirm/cancel are safe to repeat within the state machine.
5. **Migrations at startup.** The API container applies EF migrations with retry on boot,
   so `docker compose up` yields a fully provisioned database.
6. **Frontend polls** (4 s classes, 5 s my-holds, 1 s countdown) — no WebSocket complexity
   needed at this scale. Countdown hits zero → UI shows EXPIRED without reload and asks
   the server for fresh state.

## Why not microservices?

The assignment is a small admission application. Separate services for classes, holds and
students would add network partitions, distributed transactions and deployment complexity
without benefit at this scale. A modular monolith keeps a single ACID transaction for the
critical seat-reservation operation while retaining clear module boundaries that can be
extracted later if genuinely needed.
