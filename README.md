# GRA eVAT Middleware

Multi-tenant ASP.NET Core middleware that exposes a canonical API for Odoo and integrates with the Ghana Revenue Authority eVAT API.

## Solution projects

- `GraEvatMiddleware.Api` hosts the HTTP API and composition root.
- `GraEvatMiddleware.Application` contains use cases and integration abstractions.
- `GraEvatMiddleware.Domain` contains tenant and transaction business rules.
- `GraEvatMiddleware.Infrastructure` contains persistence, authentication, secrets and operational adapters.
- `GraEvatMiddleware.GraClient` isolates the GRA v8.2 HTTP contract and mapping.

## Implemented baseline

- Tenant registration, activation, suspension, irreversible termination and configuration updates
- Per-tenant API key issuance, atomic rotation and revocation with HMAC-SHA256 hashing
- Canonical invoice submission endpoint for Odoo, Python and other HTTP clients
- Tenant-scoped idempotency and transaction lookup
- PostgreSQL persistence, initial EF Core migration and audit events
- Sanitized inbound and outbound request/response exchange logs
- GRA v8.2 invoice adapter with secret indirection and conservative timeout handling
- Durable GRA retry jobs with lease-based multi-instance claiming and callback-first reconciliation
- Correlation IDs, RFC 7807 errors and health endpoint
- Tenant-scoped audit lookup and configurable request rate limiting
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
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5434;Database=gra_evat;Username=gra_evat_app;Password=<password>" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "Security:AdminKey" "<admin-key>" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "Security:ApiKeyPepper" "<random-pepper>" --project src/GraEvatMiddleware.Api
```

Apply the database migration and start the API:

```powershell
dotnet tool run dotnet-ef database update --project src/GraEvatMiddleware.Infrastructure --startup-project src/GraEvatMiddleware.Api
dotnet run --project src/GraEvatMiddleware.Api
```

When the API is started from Visual Studio in the Development environment, the browser opens the interactive Swagger UI automatically. If it does not, open the `/swagger` path shown against the API URL in the Visual Studio output, for example `http://localhost:5189/swagger` for the HTTP profile.

## Test the local workflow in Swagger

Keep `GraApi:Enabled` set to `false` while using this workflow. This saves the local transaction but prevents any request from being sent to GRA.

1. Expand `GET /api/v1/health/live`, select **Try it out**, then select **Execute**. A working API returns HTTP `200` and `status: healthy`.
2. Expand `GET /api/v1/health/ready` and execute it. HTTP `200` confirms PostgreSQL is reachable and GRA is either intentionally disabled or healthy. HTTP `503` identifies a dependency that must be corrected before invoice traffic is accepted.
3. Select **Authorize**. Enter the value of your `Security:AdminKey` user secret in `AdminKey`; enter the value only, without an `X-Admin-Key:` prefix.
4. Expand `POST /api/v1/admin/tenants`, select **Try it out**, and register a local tenant with the body below. Use a new `slug` and `taxpayerReference` if you repeat this test.

```json
{
  "name": "Local Test Company",
  "slug": "local-test-company-001",
  "taxpayerReference": "C000000000001",
  "graSecurityKeySecretName": "local-gra-staging-key"
}
```

The secret name is only a reference label while GRA integration is disabled. The response contains `credential.apiKey`; copy it immediately because the application stores only its hash and cannot display that key again.

5. Select **Authorize** again and enter the returned `credential.apiKey` in `TenantApiKey`; enter the key value only, without an `X-API-Key:` prefix.
6. Expand `POST /api/v1/invoices`, select **Try it out**, set `Idempotency-Key` to a unique value such as `local-invoice-0001`, and submit the controlled invoice below.

```json
{
  "invoiceNumber": "LOCAL-0001",
  "currency": "GHS",
  "exchangeRate": 1,
  "calculationType": "INCLUSIVE",
  "transactionDate": "2026-11-03",
  "totalVat": 0,
  "totalLevy": 0,
  "totalAmount": 100,
  "businessPartnerName": "Local Test Customer",
  "businessPartnerTin": "C000000000002",
  "saleType": "NORMAL",
  "discountType": null,
  "discountAmount": 0,
  "groupReferenceId": null,
  "items": [
    {
      "itemCode": "ITEM-001",
      "itemCategory": "",
      "description": "Controlled local test item",
      "quantity": 1,
      "unitPrice": 100,
      "levyAmountA": 0,
      "levyAmountB": 0,
      "levyAmountC": 0,
      "levyAmountD": 0,
      "levyAmountE": 0,
      "discountAmount": 0,
      "exciseAmount": 0,
      "batchCode": null,
      "expireDate": null
    }
  ]
}
```

The first submission returns HTTP `201`. With GRA disabled, the transaction remains `PendingGra` with `GRA_INTEGRATION_DISABLED`; that result confirms the safety switch worked. Copy the returned `transactionId`.

7. Expand `GET /api/v1/transactions/{transactionId}`, select **Try it out**, paste the transaction ID, and execute it. This confirms the transaction was persisted for the authenticated tenant.
8. Confirm the sanitized inbound exchanges in PostgreSQL with this one-line command from PowerShell:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -p 5434 -U gra_evat_app -d gra_evat -W -c 'SELECT "Direction", "Operation", "ResponseStatusCode", "Outcome", "ErrorCode", "CreatedAt" FROM integration_exchange_logs ORDER BY "CreatedAt" DESC LIMIT 20;'
```

No outbound GRA exchange is expected while `GraApi:Enabled` is `false`, because the safety switch stops the client before an HTTP request is created.

For a configured tenant, store its GRA security key under the secret name recorded at registration:

```powershell
dotnet user-secrets set "GraSecrets:<secret-name>" "<GRA-staging-security-key>" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "GraApi:BaseUrl" "https://vsdcstaging.vat-gh.com/" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "GraApi:Environment" "Staging" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "GraApi:AllowedHosts:0" "vsdcstaging.vat-gh.com" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "GraApi:EnableStagingIntegrationTests" "true" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "GraStaging:TaxpayerReference" "<approved-staging-reference>" --project src/GraEvatMiddleware.Api
dotnet user-secrets set "GraStaging:SecurityKeySecretName" "<secret-name>" --project src/GraEvatMiddleware.Api
```

The client refuses non-HTTPS URLs, non-staging environments and hosts not listed in `GraApi:AllowedHosts`. Redirect following is disabled so the `security_key` header cannot be forwarded to a different host.

Run the real staging test only when the configuration above points to the documented GRA staging environment. It checks GRA health, submits one synthetic invoice with a unique `EVAT-STAGE-<timestamp>` number, requests the invoice callback and verifies captured logs do not contain the security key:

```powershell
$env:GRA_STAGING_TESTS_ENABLED='true'; $env:GraApi__Enabled='true'; dotnet test .\tests\GraEvatMiddleware.IntegrationTests\GraEvatMiddleware.IntegrationTests.csproj --filter "Category=GraStaging"; Remove-Item Env:GRA_STAGING_TESTS_ENABLED; Remove-Item Env:GraApi__Enabled
```

The staging test is excluded from ordinary test runs to prevent accidental invoice submissions. `GraApi:Enabled` remains false in tracked and local configuration; the command enables it only for that test process.

## GRA retry and reconciliation

`GraRetry:Enabled` controls creation of durable retry jobs. `GraRetry:WorkerEnabled` controls the background processor separately, allowing jobs and migrations to be inspected before processing begins.

The worker always calls the documented GRA invoice callback before considering a resubmission. An ambiguous timeout or connection failure is never automatically resubmitted. Only a temporary failure may be resubmitted, and only after callback reports that the invoice was not found. Claims use an atomic conditional PostgreSQL update with a lease, so concurrent middleware instances cannot process the same job. Expired leases can be reclaimed after a crashed worker.

Apply the reviewed retry migration with this one-line PowerShell command:

```powershell
dotnet tool run dotnet-ef database update --project .\src\GraEvatMiddleware.Infrastructure --startup-project .\src\GraEvatMiddleware.Api
```

Inspect retry jobs with this one-line PowerShell command:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -p 5434 -U gra_evat_app -d gra_evat -W -c 'SELECT "TransactionId", "TenantId", "Status", "FailureClassification", "AttemptNumber", "MaxAttempts", "NextAttemptAt", "LastErrorCode" FROM gra_retry_jobs ORDER BY "CreatedAt" DESC LIMIT 20;'
```

The persisted canonical retry payload contains invoice data needed for a safe resubmission. Production deployment therefore requires database encryption, least-privilege database access and an approved retention period.

The canonical contract is documented in [docs/api-contract-v1.md](docs/api-contract-v1.md) and [docs/openapi-v1.yaml](docs/openapi-v1.yaml).

Operational guidance is in [docs/operations-runbook.md](docs/operations-runbook.md), [docs/tenant-onboarding.md](docs/tenant-onboarding.md), [docs/threat-model.md](docs/threat-model.md) and [docs/test-evidence.md](docs/test-evidence.md).

Reusable API checks are documented in [docs/postman-testing.md](docs/postman-testing.md). The isolated PostgreSQL concurrency suite is documented in [docs/postgresql-concurrency-testing.md](docs/postgresql-concurrency-testing.md).

## Rate limiting

Rate limiting is enabled by default and configured without code changes:

```json
"RateLimiting": {
  "Enabled": true,
  "PermitLimit": 120,
  "WindowSeconds": 60,
  "QueueLimit": 0
}
```

The partition key is a SHA-256 fingerprint of the supplied middleware credential, or the remote address for unauthenticated requests. The raw key is not retained. A rejected request returns HTTP 429 with code `RATE_LIMIT_EXCEEDED`.

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
