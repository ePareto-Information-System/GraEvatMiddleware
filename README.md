# GRA eVAT Middleware

Multi-tenant ASP.NET Core middleware that exposes a canonical API for Odoo and integrates with the Ghana Revenue Authority eVAT API.

## Solution projects

- `GraEvatMiddleware.Api` hosts the HTTP API and composition root.
- `GraEvatMiddleware.Application` contains use cases and integration abstractions.
- `GraEvatMiddleware.Domain` contains tenant and transaction business rules.
- `GraEvatMiddleware.Infrastructure` contains persistence, authentication, secrets and operational adapters.
- `GraEvatMiddleware.GraClient` isolates the GRA v8.2 HTTP contract and mapping.

## Implemented baseline

- Tenant registration, activation and suspension
- Per-tenant API key issuance and revocation with HMAC-SHA256 hashing
- Canonical invoice submission endpoint for Odoo, Python and other HTTP clients
- Tenant-scoped idempotency and transaction lookup
- PostgreSQL persistence, initial EF Core migration and audit events
- GRA v8.2 invoice adapter with secret indirection and conservative timeout handling
- Correlation IDs, RFC 7807 errors and health endpoint
- Domain, architecture and HTTP workflow tests

The GRA integration is disabled by default. Enable it only after supplying the approved base URL and credentials for the target GRA environment.

## Build

```powershell
dotnet tool restore
dotnet build GraEvatMiddleware.slnx
dotnet test GraEvatMiddleware.slnx
```

## Local configuration

Start PostgreSQL with Docker Compose:

```powershell
Copy-Item .env.example .env
# Replace the placeholder in .env before continuing.
docker compose up -d database
```

Keep credentials outside tracked configuration. For local development, initialize user secrets and set your own values:

```powershell
dotnet user-secrets init --project src/GraEvatMiddleware.Api
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=gra_evat;Username=postgres;Password=<password>" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "Security:AdminKey" "<admin-key>" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "Security:ApiKeyPepper" "<random-pepper>" --project src/GraEvatMiddleware.Api
```

Apply the database migration and start the API:

```powershell
dotnet tool run dotnet-ef database update --project src/GraEvatMiddleware.Infrastructure --startup-project src/GraEvatMiddleware.Api
dotnet run --project src/GraEvatMiddleware.Api
```

For a configured tenant, store its GRA security key under the secret name recorded at registration:

```powershell
dotnet user-secrets set "GraSecrets:<secret-name>" "<GRA-security-key>" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "GraApi:BaseUrl" "<approved-GRA-base-URL>" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "GraApi:Enabled" "true" --project src/GraEvatMiddleware.Api
```

The canonical contract is documented in [docs/api-contract-v1.md](docs/api-contract-v1.md) and [docs/openapi-v1.yaml](docs/openapi-v1.yaml).
