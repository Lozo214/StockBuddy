# Stock Buddy

A mobile-friendly inventory tracker built with C#, ASP.NET Core 10, Blazor Server, Entity Framework Core, and SQLite.

Live application: https://stockbuddy.77-112-80-43.sslip.io

## Features

- Username/password accounts through ASP.NET Core Identity.
- Private inventories, with ownership checked on every read, update, and deletion.
- Optional remembered sign-in and sign-out across devices.

- Two-column Item / Count table, with quantity adjustments stacked inside Count.
- Add items, edit names and quantities, and remove items with confirmation.
- Automatic database saves when leaving a field; plus/minus operations save immediately.
- SQLite persistence across page refreshes and application restarts.
- Nonnegative integer counts, positive adjustments, overflow protection, and 120-character item names.
- Visible save status and recoverable error feedback.
- Optimistic concurrency: stale tabs cannot silently overwrite or delete changed rows.
- Responsive layout, labeled inputs, keyboard focus indicators, and 44px controls.
- Inventory totals and out-of-stock summary.
- Database-aware /health endpoint.

## Run

Install the .NET 10 SDK. From this directory:

```powershell
dotnet run --project src/StockBuddy.Web -- --urls http://localhost:5265
```

Open http://localhost:5265, choose Create an account, and keep the terminal running. Ctrl+C stops it.
Use a username of 3–40 letters/numbers/dots/underscores/hyphens and a password of 12–128 characters.
There is no email verification or password recovery yet.
On the development machine, ./Start-StockBuddy.ps1 also supports the workspace-local SDK in ../.tools/dotnet.

The inventory database is created automatically at src/StockBuddy.Web/stockbuddy.db.
Accounts and password hashes are stored separately in src/StockBuddy.Web/accounts.db.
Back up and deploy both databases together. Passwords are hashed by Identity, never stored as plaintext.
Relative database paths are resolved against the application's content root.
Connections can be overridden with ConnectionStrings__StockBuddy and ConnectionStrings__Accounts.
When the Accounts setting is absent, accounts.db is placed beside the inventory database.

## Verify

```powershell
dotnet test StockBuddy.slnx -c Release
dotnet publish src/StockBuddy.Web -c Release -o artifacts/app
```

The tests use isolated, real SQLite files. They cover CRUD persistence, stale edits/deletes, deletion conflicts, invalid values, name limits, subtraction clamping, overflow, ownership isolation, and preservation of old shared records.
HTTP integration tests verify registration, login, remembered cookies, CSRF protection, duplicate usernames, password rules, lockout, sign-out, and security-stamp revocation.

Manual check: add an item, enter 12, set adjustment to 5, click +, then reload.
The quantity should remain 17. Restart the server and confirm again.

## Docker

```powershell
docker compose up --build -d
```

Open http://localhost:8080. The container runs as a non-root user and stores both SQLite databases and cookie-protection keys in a named volume.
Compose uses Development for the loopback-only HTTP preview. Deploy with Production and HTTPS; production account cookies require HTTPS.
Docker packaging is provided but has not been executed on the development machine (Docker is not installed).
Do not use docker compose down --volumes unless you intend to erase the inventory.

## Architecture

Browser → Blazor interactive server component → InventoryService → EF Core → SQLite

- Components/Pages/Home.razor: UI events and save/error feedback.
- Services/InventoryService.cs: persistence, validation, and concurrency checks.
- Data/StockBuddyDbContext.cs: relational model and database constraints.
- Models/StockItem.cs: inventory record and concurrency token.
- Program.cs: configuration, database factory, startup schema, and health endpoint.

Each database operation uses a fresh context. The component never holds a shared long-lived context.
Each record has a version token that changes after a successful save.
The GitHub workflow builds, tests, and publishes when this folder is used as the repository root.
It has not been run on GitHub yet.

## Current scope and deployment

This version has separate user accounts and private inventories and is deployed to a single Amazon EC2 instance in AWS Ohio.
The previous shared inventory is preserved with no owner and hidden from all accounts, never automatically given to the first registrant.
The pre-upgrade local backup is artifacts/backups/stockbuddy-before-accounts.db.
Account creation uses usernames; email verification, password recovery, MFA, and account deletion are not implemented.
Signing out revokes all sessions for that user. Existing circuits validate the security stamp on every inventory operation and revalidate their UI at least once a minute.
Account forms have CSRF protection, login lockout (5 failures / 15 minutes), and a per-IP request limit (30 requests / minute).
Blazor Server needs a live connection; offline editing is not supported.
Edits are committed on field blur, rather than on every keystroke. Wait for Saved before closing the tab.
There is no fixed row limit, but the current UI loads all rows and is intended for small inventories.
Other tabs must reload to see changes; conflicts are reported rather than silently overwriting.

SQLite is appropriate for a single app instance with persistent disk. Do not place it on ephemeral cloud container storage or scale this version across multiple replicas.
The prototype uses EnsureCreated for its initial schemas plus one idempotent, additive OwnerId upgrade for the old inventory database.
Adopt versioned EF migrations before further schema evolution in production. Startup upgrades are for a single instance.
Back up both databases with the app stopped (or use SQLite's backup facilities); avoid copying a live WAL database file alone.
The deployment persists both databases and the Data Protection keys on an encrypted 20 GiB EBS volume. DataProtection__KeyPath configures the key location.

See docs/DEPLOYMENT.md for the AWS path and docs/RESUME.md for accurate project wording.
