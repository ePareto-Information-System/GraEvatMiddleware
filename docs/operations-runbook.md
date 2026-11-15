# Operations Runbook

## Service controls

Keep `GraApi:Enabled`, `GraRetry:Enabled` and `GraRetry:WorkerEnabled` disabled until the target environment and credentials are verified. Enable retry scheduling before enabling the worker so queued jobs can be inspected.

Liveness is available at `GET /api/v1/health/live`. A healthy response proves the process is serving requests; it does not prove PostgreSQL or GRA availability. Readiness is available at `GET /api/v1/health/ready`. It verifies PostgreSQL and, when GRA integration is enabled, uses the first active tenant configuration to call the GRA health operation. HTTP 503 means the instance should not receive invoice traffic until the reported component is corrected.

Aggregate operational status is available at `GET /api/v1/admin/operations/status` and requires `X-Admin-Key`. It reports request totals, server failures, rate-limit rejections, average request duration, pending transactions, retry states, exhausted jobs and expired leases without returning tenant payloads or credentials.

Startup validation rejects missing, short, placeholder or matching administrative keys and API-key peppers. When GRA is enabled, startup also requires the staging environment, HTTPS, an allowlisted approved staging/test host, and a timeout between 1 and 120 seconds. Keep these values in user secrets or the approved environment secret store, never committed settings.

## Deployment

1. Back up PostgreSQL and record the deployed commit and configuration version.
2. Build and test the exact release revision.
3. Apply EF Core migrations once using the deployment identity.
4. Deploy with secrets supplied by user secrets only for development or by the approved production secret store.
5. Verify liveness, authentication failure, tenant authentication and transaction lookup.
6. Enable GRA and retry controls only after staging or production authorization.

Release build command: `dotnet build .\GraEvatMiddleware.slnx --configuration Release --no-restore`

Migration command: `dotnet tool run dotnet-ef database update --project .\src\GraEvatMiddleware.Infrastructure --startup-project .\src\GraEvatMiddleware.Api`

## Monitoring

Monitor HTTP 5xx and 429 rates, GRA error codes, pending transactions, exhausted retry jobs, retry lease age, authentication failures, database capacity and exchange-log growth. Alert when a processing lease remains expired, retries are exhausted, or pending transactions exceed the approved reconciliation time.

Console output is structured JSON. Each completed request log includes the correlation ID, HTTP method, path, status and elapsed time. Forward these logs to the approved central logging platform and poll the protected operations status endpoint from the approved internal monitoring network.

Exchange-log cleanup runs as a hosted service. `ExchangeLogging:RetentionDays` defines the age threshold, `RetentionCleanupInitialDelaySeconds` controls startup delay and `RetentionCleanupIntervalMinutes` controls the recurring schedule. Keep cleanup enabled unless an approved external retention process owns deletion, and alert on cleanup failures.

## Backup and recovery

Back up the database with encrypted PostgreSQL backups and test restoration into an isolated environment. A restore test must confirm tenants, credential hashes, transactions, audit events, exchange logs, retry jobs and migration history. Raw secret values are restored from the secret manager, not from database backups.

Follow `recovery-and-continuity.md` for the backup template, isolated restore safety checks, outage ordering and drill evidence. Never restore over the live database as a test.

## Incident handling

For a compromised tenant key, rotate or revoke the credential and inspect tenant-scoped audit/exchange records. For a compromised GRA key, disable GRA integration, rotate the external secret, update only the secret-store value and reconcile pending transactions before re-enabling. For uncertain invoice delivery, never submit manually with a new idempotency key; use transaction status and callback reconciliation.

Use `incident-response.md` for containment, evidence, escalation and recovery. Keep contact details in the organization-controlled incident directory rather than source control.

## Rollback

Roll back application binaries only when the previous release is schema-compatible. Do not automatically roll back a migration containing production data. Restore from backup only through an approved recovery decision, then reconcile any transaction whose GRA outcome may have occurred after the backup time.

Use `release-checklist.md` for every release and `production-readiness-checklist.md` for the go-live gate. Track external sign-offs and exercises in `external-evidence-register.md`.
