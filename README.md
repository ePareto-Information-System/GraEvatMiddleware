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
- Sanitized inbound and outbound request/response exchange logs
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

Create the `gra_evat` database and `gra_evat_app` login on the local PostgreSQL 18 server. Keep credentials outside tracked configuration. Initialize user secrets and set your own values:

```powershell
dotnet user-secrets init --project src/GraEvatMiddleware.Api
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=gra_evat;Username=gra_evat_app;Password=<password>" --project src/GraEvatMiddleware.Api
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

## Request and response exchange logs

The `integration_exchange_logs` PostgreSQL table records sanitized inbound middleware exchanges and outbound GRA exchanges. Records include tenant, transaction, correlation and idempotency identifiers; direction; operation; path; HTTP status; duration; outcome; and optional request/response bodies.

The logger always redacts authentication headers, cookies, passwords, API keys, GRA security keys, tokens, secrets and connection strings. JSON redaction is recursive and case-insensitive. Unsupported binary content is not stored. Do not treat this table as a substitute for database encryption, access controls or an approved taxpayer-data retention policy.

Configure capture in `appsettings.json` or environment-specific configuration:

```json
"ExchangeLogging": {
  "Enabled": true,
  "CaptureBodies": true,
  "MaximumBodyCharacters": 65536,
  "RetentionDays": 90
}
```

Set `CaptureBodies` to `false` to retain exchange metadata without bodies. Set `Enabled` to `false` to disable exchange persistence. Retention is exposed through an application service and is not scheduled automatically.

Example troubleshooting query:

```sql
SELECT "CreatedAt", "Direction", "Operation", "ResponseStatusCode", "Outcome", "ErrorCode"
FROM integration_exchange_logs
WHERE "CorrelationId" = 'your-correlation-id'
ORDER BY "CreatedAt";
```
