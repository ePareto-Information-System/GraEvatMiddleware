# Operations Runbook

## Service controls

Keep `GraApi:Enabled`, `GraRetry:Enabled` and `GraRetry:WorkerEnabled` disabled until the target environment and credentials are verified. Enable retry scheduling before enabling the worker so queued jobs can be inspected.

Liveness is available at `GET /api/v1/health/live`. A healthy response proves the process is serving requests; it does not prove PostgreSQL or GRA availability.

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

## Backup and recovery

Back up the database with encrypted PostgreSQL backups and test restoration into an isolated environment. A restore test must confirm tenants, credential hashes, transactions, audit events, exchange logs, retry jobs and migration history. Raw secret values are restored from the secret manager, not from database backups.

## Incident handling

For a compromised tenant key, rotate or revoke the credential and inspect tenant-scoped audit/exchange records. For a compromised GRA key, disable GRA integration, rotate the external secret, update only the secret-store value and reconcile pending transactions before re-enabling. For uncertain invoice delivery, never submit manually with a new idempotency key; use transaction status and callback reconciliation.

## Rollback

Roll back application binaries only when the previous release is schema-compatible. Do not automatically roll back a migration containing production data. Restore from backup only through an approved recovery decision, then reconcile any transaction whose GRA outcome may have occurred after the backup time.
