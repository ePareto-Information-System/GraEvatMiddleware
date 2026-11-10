# Automated Test Evidence

## Test boundaries

Ordinary test runs use an in-memory database and a controlled GRA substitute. They do not transmit tax invoices. The separately gated staging test uses the documented GRA staging health, invoice and callback endpoints and runs only when staging is explicitly enabled.

## Covered behaviours

- Tenant registration, activation, suspension and termination
- API-key issuance, atomic rotation, revocation and authentication
- Tenant configuration changes and audit creation
- Tenant-isolated transaction and audit lookup
- Validation, authentication, conflict and rate-limit problem codes
- Tenant-scoped idempotent replay and changed-payload rejection
- Refund, partial-refund, purchase, purchase-return and cancellation routing with exact GRA flags
- Credit/debit-note, statement-of-account and inventory routing with exact published paths and payload fields
- TIN and Ghana Card route mapping and normalized identification responses
- Sanitized inbound/outbound logging and retention deletion
- GRA success, rejection, timeout and temporary-failure handling
- Callback-first ambiguous reconciliation
- Temporary retry only after callback reports invoice not found
- Retry leasing, attempt limits and exhaustion state
- GRA staging host, HTTPS and environment safety controls
- GRA error classification for already-processed, not-found, credential, rate-limit, service and malformed-response cases
- Startup validation for administrative keys, API-key pepper and enabled GRA settings
- Liveness and dependency-readiness responses
- Admin-protected operational request and retry-queue status
- Structured request telemetry with correlation IDs and duration
- Hosted exchange-log retention cleanup registration
- Architecture dependency boundary
- PostgreSQL-only concurrency checks for duplicate submission, retry-worker claiming and expired-lease recovery
- Postman contract and local workflow linting for all 24 operations and nine reusable regression requests

The release verification run completed 97 passing tests: 44 unit tests, 52 integration tests and one architecture test. Three PostgreSQL concurrency tests remain gated by an isolated test-database connection string. The real GRA staging test also remains skipped unless explicitly enabled with approved staging configuration.

## Manual evidence still required

- Odoo to middleware to GRA to Odoo end-to-end validation
- Representative PostgreSQL load and endurance testing beyond the implemented concurrency checks
- Backup restoration and disaster-recovery exercise
- Production hosting, proxy and TLS validation
- Vulnerability assessment and penetration testing
- Pilot tenant acceptance and GRA production certification
