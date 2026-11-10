# API Specification

Base URL: `http://localhost:8080` — JSON over HTTP.

Student identity is passed via the `X-Student-Id` header (17-digit birth registration number)
on hold create, confirm, cancel and "my applications". `GET /classes*` needs no header.

All error responses use RFC 7807 Problem Details:

```json
{
  "type": "https://school-admission.local/errors/seat-unavailable",
  "title": "No seats available",
  "status": 409,
  "detail": "No seats are currently available for this class."
}
```

## Endpoints

| # | Method | Path | Purpose | Success |
|---|---|---|---|---|
| 1 | POST | `/classes` | Create class | 201 |
| 2 | GET | `/classes` | List classes with seat counts | 200 |
| 3 | GET | `/classes/{classId}` | Class details | 200 |
| 4 | POST | `/classes/{classId}/holds` | Create hold | 201 |
| 5 | GET | `/holds/{holdId}` | Hold details | 200 |
| 6 | POST | `/holds/{holdId}/confirm` | Confirm admission | 200 |
| 7 | DELETE | `/holds/{holdId}` | Cancel hold | 204 |
| 8 | GET | `/me/holds` | Student's applications | 200 |

Status codes: 400 invalid input, 404 missing class/hold, 403 not owner, 409 seat
unavailable / duplicate application / invalid transition, 201 created, 200 ok, 204 no content.

### 1. Create class

`POST /classes`

```json
{ "name": "Class 6 - Morning Shift", "totalSeats": 120 }
```

201:

```json
{ "id": "…", "name": "Class 6 - Morning Shift", "totalSeats": 120 }
```

### 2. List classes

`GET /classes` → 200:

```json
[
  {
    "id": "…",
    "name": "Class 6 - Morning Shift",
    "totalSeats": 120,
    "heldSeats": 15,
    "confirmedSeats": 80,
    "availableSeats": 25
  }
]
```

`heldSeats` counts non-expired `HELD` rows only.

### 3. Class details

`GET /classes/{classId}` → 200 (same shape as one list item). 404 if unknown.

### 4. Create hold

`POST /classes/{classId}/holds` with header `X-Student-Id: 20152691234567890` → 201:

```json
{
  "holdId": "…",
  "classId": "…",
  "studentId": "20152691234567890",
  "status": "HELD",
  "expiresAt": "2026-11-01T04:10:00Z"
}
```

404 unknown class; 409 no seats available or duplicate active application; 400 invalid/missing student ID.

### 5. Get hold

`GET /holds/{holdId}` → 200:

```json
{ "holdId": "…", "classId": "…", "status": "HELD", "expiresAt": "2026-11-01T04:10:00Z" }
```

404 if unknown. Per the assignment, this endpoint does not require the student header.

### 6. Confirm hold

`POST /holds/{holdId}/confirm` with `X-Student-Id` → 200:

```json
{ "holdId": "…", "status": "CONFIRMED" }
```

403 not owner; 404 unknown; 409 expired/cancelled hold or expiry discovered at confirm time.

### 7. Cancel hold

`DELETE /holds/{holdId}` with `X-Student-Id` → 204 (no body).

403 not owner; 404 unknown; 409 invalid transition (e.g. cancelling a CONFIRMED or EXPIRED hold);
idempotent 204 when already cancelled.

### 8. My applications

`GET /me/holds` with `X-Student-Id` → 200:

```json
[
  {
    "holdId": "…",
    "classId": "…",
    "className": "Class 6 - Morning Shift",
    "status": "HELD",
    "expiresAt": "2026-11-01T04:10:00Z",
    "createdAt": "2026-11-01T04:00:00Z"
  }
}
```

Status values: `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`. Expired-on-server rows are
reported as `EXPIRED` even before the background worker has flipped them (lazy evaluation).

## Notes

- IDs are UUID strings.
- The response shapes above are contractual; they are never wrapped in an envelope.
- Never return raw database errors or stack traces to clients.
