# Assumptions

1. **One application holds one seat.** A seat is considered occupied while an application is `HELD` and has not expired, or when it is `CONFIRMED`.

   Available seats are calculated as:

   `Available Seats = Total Seats - Held Seats - Confirmed Seats`

   These numbers are calculated from the database records instead of being stored separately. This prevents incorrect seat counts.

2. **Seat limits are protected, even with multiple API servers.**

   When a student applies, the system locks the selected class record, checks the available seats, and creates the hold within one database transaction.

   This prevents two students from taking the same remaining seat at the same time. It also works if multiple API processes are running.

3. **Application expiry is controlled by the database clock.**

   The system uses PostgreSQL's clock instead of the API server's clock to calculate expiry times.

   Once a hold expires, it no longer occupies a seat and its status is treated as `EXPIRED`. A background worker updates the stored status, but seat availability does not depend on that worker.

4. **A student can have only one active application per class.**

   A student cannot create another active application for the same class while their existing application is `HELD` or `CONFIRMED`.

   However, after an application is `CANCELLED` or `EXPIRED`, the student can apply again. A student can also apply to different classes at the same time.

5. **Confirmation and cancellation rules.**

   - **Confirm:** Confirming an application more than once does not create duplicate admissions. A successful confirmation returns `200`.
   - **Expired application:** An expired application cannot be confirmed and returns `409 HOLD_EXPIRED`.
   - **Ownership:** A student cannot confirm or cancel another student's application. This returns `403`.
   - **Cancel:** A pending (`HELD`) application can be cancelled. Repeating the cancellation is also allowed and returns `204`.
   - **Confirmed application:** A confirmed admission cannot be cancelled through this endpoint and returns `409`.

6. **No login or authentication is required.**

   The assignment does not require a login system. The 17-digit `X-Student-Id` header is used to identify students.

   The class creation screen and API are also accessible without authentication.

   In a real production application, student authentication and admin permissions would be required.

   `GET /holds/{id}` does not require a student ID header because the assignment does not specify one. It also does not return the student's ID.

7. **ID, validation, and date formats.**

   - Class and application IDs use UUID format.
   - Class names must contain between 1 and 200 characters.
   - Seat counts must be between 1 and 100000.
   - All timestamps are stored and returned in UTC using ISO 8601 format.

8. **Database structure is created automatically.**

   The required database tables are created when the API starts.

   Instead of using Entity Framework migrations, the project uses an SQL schema script. A PostgreSQL advisory lock prevents multiple API processes from trying to initialize the database at the same time.

   This approach keeps the database setup simple for this project.

9. **CORS configuration.**

   CORS allows requests from any origin so the React frontend can communicate with the API from different hosts or ports.

   For a production application, this should be restricted to trusted frontend domains.

10. **Frontend updates and countdown.**

    The frontend checks for updated application and seat information every 3 seconds.

    The countdown updates every second and uses the server's clock information to avoid problems caused by an incorrect computer clock.

    The server response is always treated as the final source of truth.

11. **Project structure.**

    The backend uses one ASP.NET Core API project with separate folders for models, services, and background workers.

    A full Clean Architecture structure was not used because this project has a relatively small domain and only a few database tables. Keeping the structure simple makes the code easier to navigate and maintain.