# AI Usage

This project was developed with AI assistance for requirement understanding, technical guidance, debugging, and implementation support. AI tools were used as development assistants throughout different stages of the project.

## Tool used

- **ChatGPT** – Used to explain the assignment requirements in detail, break down complex tasks into smaller steps, discuss technical approaches, and troubleshoot development issues.
- **OpenCode** – Used as an interactive CLI coding assistant for implementation suggestions, code generation, and debugging across multiple sessions.

## What AI did

- Helped interpret the assignment brief and understand the expected application workflow, including first-come-first-served seat allocation, temporary holds, expiry, and admission confirmation.
- ChatGPT helped organize the development process into three main phases:
  - **Backend and API:** Planning the API structure, understanding endpoint responsibilities, validation, and admission-related business logic.
  - **Database:** Understanding PostgreSQL schema design, relationships, transactions, concurrency handling, and automatic hold expiry.
  - **Frontend and Docker:** Planning the React interface, connecting frontend components with the API, and configuring Docker to run the complete application.
- Explained technical concepts and discussed implementation approaches, especially PostgreSQL concurrency, API architecture, and Docker configuration.
- Provided implementation suggestions across different parts of the project:
  - Backend: ASP.NET Core Minimal API, Dapper, Npgsql, request validation, and service structure.
  - Database: PostgreSQL schema, SQL queries, transactions, row locking, and constraints.
  - Admission logic: Temporary holds, expiry, confirmation, cancellation, and duplicate application prevention.
  - Frontend: React 18 components, API integration, polling, and countdown handling.
  - Infrastructure: Dockerfiles, Docker Compose, environment variables, and service dependencies.
  - Testing: Backend integration tests, frontend tests, and concurrency scenarios.
- Assisted with reviewing error messages, understanding test failures, and finding possible improvements during development.

## What I did myself

- Reviewed the assignment requirements and made decisions about how the application should behave.
- Used the technical explanations to understand the backend, database, frontend, and Docker implementation.
- Reviewed the important SQL transactions and admission-related business logic.
- Made decisions where the assignment was ambiguous, including idempotent confirmation, cancelling a confirmed admission returning 409, `GET /holds/{id}` not requiring a student header, and using UUID identifiers.
- Ran the application locally, tested API endpoints through Swagger, and checked the frontend.
- Ran automated tests and investigated issues when tests or application features did not work as expected.
- Reviewed AI-generated suggestions and adjusted the implementation where necessary.

## Where the AI was wrong / what I corrected

- **API architecture:** Some initial suggestions about endpoint implementation and request handling did not work as expected with Minimal APIs and Swagger. For example, manually reading request bodies and headers made some fields unavailable in Swagger. I adjusted the endpoint definitions to expose the request body and required headers properly.
- **Docker configuration:** Some initial Docker configuration and build suggestions needed adjustments to work correctly in my local environment. I reviewed the Dockerfiles, environment variables, service dependencies, and startup configuration.
- **Admission logic:** Some suggested approaches needed further review to ensure that expired holds did not occupy seats, duplicate active applications were prevented, and concurrent requests could not exceed available seat capacity.
- **Runtime compatibility:** An initial backend test run using the .NET 9 runtime caused a compatibility issue with the ASP.NET Core test host. Running the tests with the intended .NET 8 runtime resolved the issue.
- **Docker networking:** Docker image downloads experienced TLS handshake timeouts on my regular Wi-Fi connection. Testing with a mobile hotspot showed that the issue was related to the network connection rather than the application code.

These issues required checking the suggestions against the actual project, testing the behavior, and making corrections where necessary.

## How everything was verified

- `dotnet build` — Verified that the backend builds successfully.
- `cd backend && dotnet test tests/SchoolAdmission.Tests` — Ran 9 backend integration tests against PostgreSQL using Testcontainers. The tests cover class creation, seat capacity, concurrent requests, duplicate applications, ownership validation, confirmation, and expiry scenarios.
- `cd frontend && npm ci && npm test` — Installed frontend dependencies and ran 7 Vitest tests covering countdown behavior, disabled buttons after expiry, student ID validation, and error-message handling.
- **Manual testing:** Used Swagger to test API endpoints and the React interface to test the main admission workflow, including class creation, applying for admission, confirmation, cancellation, and hold expiry. Only retain the scenarios here that you personally completed.