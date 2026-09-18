# Resume wording

## Accurate for the implemented and deployed version

**Stock Buddy — Inventory Tracking Web Application**
C#, ASP.NET Core, Blazor, Entity Framework Core, SQLite, xUnit, Docker, AWS EC2

- Developed a responsive inventory application with editable item quantities, automatic persistence, and configurable stock adjustments.
- Implemented SQLite data storage using Entity Framework Core, input validation, and optimistic concurrency checks to prevent stale updates.
- Added automated tests covering persistence, deletion, validation, concurrent edits, and quantity calculations.
- Implemented ASP.NET Core Identity accounts with password hashing, persistent sign-in cookies, CSRF protection, and per-user inventory authorization.
- Deployed the application to Amazon EC2 in a Docker container behind a Caddy HTTPS reverse proxy, with encrypted persistent storage and restricted administrative access.

Describe GitHub Actions as workflow configuration until you have executed it on GitHub.
Do not claim RDS, multiple production instances, production users, or managed-database operations; those have not been implemented.

## Interview preparation

Be ready to explain:

1. A browser connects to a Blazor Server component; C# event handlers update the inventory.
2. The service opens a fresh database context for each operation and disposes it afterward.
3. SQLite stores rows on disk, so data survives refreshes and restarts.
4. IDs distinguish records; version tokens prevent stale tabs from overwriting newer records.
5. Validation rejects negative counts and integer overflow, and SQL constraints protect stored data.
6. User IDs come from validated authentication claims, never from a caller-supplied owner field. Every inventory operation filters by that ID.
7. Signing out rotates the user's security stamp, preventing old cookies and interactive sessions from continuing to access inventory.
8. Single-instance SQLite storage and no password recovery are current limitations; the deployed version uses persistent encrypted EC2 storage.

The project was developed with AI assistance. Review the code and tests before describing implementation details in an interview.
