# Verification — September 18, 2026

- .NET 10 SDK 10.0.301 restored into the parent workspace's .tools/dotnet directory.
- Build succeeded with zero warnings and zero errors.
- Release test run: 11 passed, 0 failed.
- Release publish succeeded into artifacts/app; no database file was included.
- GET /health returned healthy.
- Browser: added Printer paper, entered count 12 and adjustment 5, clicked +; result 17.
- Reloaded browser: name, count 17, and adjustment 5 persisted.
- Stopped and restarted server: same inventory persisted.
- Invalid count -5: displayed validation error, retained database value, reset visible input to 17.
- Inspected layout at 390px; checked 320px document width without horizontal overflow.
- Left sample Printer paper row in the local database for demonstration.

Not verified: Docker execution on the development machine (Docker unavailable) or testing on a physical phone.
The above results describe the initial shared-inventory version.

## Account update

- Release tests: 17 passed, 0 failed.
- HTTP tests cover two users, isolated inventories, login/logout, remembered cookies, hashed passwords, lockout, invalid registration, duplicate names, CSRF rejection, and session revocation.
- Service tests reject anonymous access and forged owner IDs on updates/deletes.
- Legacy schema upgrade runs twice without deleting or assigning pre-existing shared records.
- Backed up the local database before upgrade to artifacts/backups/stockbuddy-before-accounts.db.
- Browser verified that anonymous inventory access redirects to sign-in and the registration page renders.
- No user account was created in the real local database by automated tests; test accounts use temporary databases.
- Email verification, password recovery, MFA, and account deletion are not implemented.

## Public release

- Deployed the application in a Docker container on an Amazon EC2 instance in AWS Ohio.
- Verified the public HTTPS health endpoint and sign-in page at https://stockbuddy.77-112-80-43.sslip.io.
- Verified that both production SQLite database files persisted through an application-container restart.
- Published the source to https://github.com/Lozo214/StockBuddy.
- GitHub Actions build-and-test run #1 completed successfully in 37 seconds.
- No production account was created during deployment verification; account behavior is covered by the automated integration tests above.
