# AWS deployment path

Status: deployed and healthy at https://stockbuddy.77-112-80-43.sslip.io.

Current AWS configuration:

- One Amazon Linux 2023 `t3.micro` instance in `us-east-2`.
- 20 GiB encrypted gp3 EBS root volume, retained if the instance is terminated.
- Fixed Elastic IP with HTTP/HTTPS open publicly; SSH is restricted to the AWS EC2 Instance Connect managed prefix list.
- Application and Caddy run as restartable Docker containers. Caddy provisions and renews the public HTTPS certificate.
- SQLite databases and ASP.NET Core Data Protection keys persist under `/opt/stockbuddy/data`.
- Termination protection is enabled and CPU credits use Standard mode.
- The tested deployment package is staged in a private S3 bucket for future updates.

## First deployment

Use a single Linux EC2 or Lightsail virtual machine with persistent disk for this SQLite version.
Run the included Docker Compose application behind a reverse proxy that terminates HTTPS and supports WebSockets.
The Compose port is bound to localhost deliberately, so a public reverse proxy must live on that host or its networking must be explicitly configured.
Application accounts are implemented with ASP.NET Core Identity and per-user inventory ownership.
Before public exposure, configure HTTPS and Production mode, persistent Data Protection keys, and backups of both databases.

Required decisions: AWS account/access, region, approved recurring budget, and domain or HTTPS gateway.
Use an IAM identity with scoped permissions; do not place credentials in source files.

## Deployment checklist

1. Provision one server only after approving its cost and disk/backup costs.
2. Install Docker using the vendor's instructions for the chosen Linux distribution.
3. Transfer the source without databases, secrets, bin, or obj directories.
4. Override the local Compose Development setting to Production and run docker compose up --build -d from the project directory.
5. Verify curl http://localhost:8080/health on the server.
6. Configure HTTPS access and WebSocket proxying; open only required firewall ports. Production account cookies require HTTPS.
7. Create test inventory through the public HTTPS URL; reload and restart the container to verify persistence.
8. Configure backups, logs, billing alerts, and a documented teardown procedure.

The named volume preserves stockbuddy.db, accounts.db, and cookie-protection keys across container replacement on the same host. It does not protect against losing the host/disk.
Both databases must be moved together for account ownership to remain valid. Never commit either database or the key directory.
The initial schema and legacy upgrade must be run by one instance. Plan EF migrations for future schema upgrades.
Forwarded headers are accepted only through ASP.NET Core's trusted loopback-proxy defaults; Caddy and the app share the host network and the app listens only on `127.0.0.1:8080`.
Account throttling remains in-process and is intended for this single-instance deployment.
Keep one app instance. A managed container service with ephemeral storage needs an external database instead.

## Later AWS milestone

Replace SQLite with PostgreSQL on RDS, adopt EF migrations, add account recovery/email verification, and deploy the container on ECS.
Use IAM roles, Secrets Manager for credentials, and CloudWatch logs. This requires implementation and verification; it is not part of the current app.

References:
- https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0
- https://docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-container-services.html
