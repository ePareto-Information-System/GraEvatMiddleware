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
- Sanitized inbound/outbound logging and retention deletion
- GRA success, rejection, timeout and temporary-failure handling
- Callback-first ambiguous reconciliation
- Temporary retry only after callback reports invoice not found
- Retry leasing, attempt limits and exhaustion state
- GRA staging host, HTTPS and environment safety controls
- GRA error classification for already-processed, not-found, credential, rate-limit, service and malformed-response cases
- Startup validation for administrative keys, API-key pepper and enabled GRA settings
- Liveness and dependency-readiness responses
- Architecture dependency boundary

The current ordinary suite contains 55 passing tests: 32 unit tests, 22 integration tests and one architecture test. The separate real GRA staging test remains skipped unless explicitly enabled with approved staging configuration.

## Manual evidence still required

- Odoo to middleware to GRA to Odoo end-to-end validation
- PostgreSQL multi-instance retry concurrency and representative load testing
- Backup restoration and disaster-recovery exercise
- Production hosting, proxy and TLS validation
- Vulnerability assessment and penetration testing
- Pilot tenant acceptance and GRA production certification
