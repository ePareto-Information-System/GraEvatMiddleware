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
- Safe Postman performance workload covering reads, validation rejection and idempotent replay without GRA traffic

The final Release verification run completed 113 passing tests: 50 unit tests, 62 integration tests and one architecture test. Four tests were intentionally skipped: three PostgreSQL concurrency tests require an isolated test-database connection string, and the real GRA staging test requires explicit enablement with approved staging configuration. The Release build completed with zero warnings and zero errors. The NuGet vulnerability scan reported no known vulnerable direct or transitive packages from the configured sources.

## Manual evidence still required

- Odoo to middleware to GRA to Odoo end-to-end validation
- Representative PostgreSQL load and endurance testing beyond the implemented concurrency checks
- Backup restoration and disaster-recovery exercise
- Production hosting, proxy and TLS validation
- Vulnerability assessment and penetration testing
- Pilot tenant acceptance and GRA production certification

## Local performance evidence

The safe one-VU fixed Postman run completed 810 requests in 61 seconds at 13.28 requests/second with 0 percent errors. Average latency was 9 ms, p90 12 ms, p95 14 ms, p99 24 ms and maximum 116 ms. A five-VU attempt was stopped by Postman when the local generator reached 100 percent CPU after 520 error-free requests; it is recorded as generator-limited rather than a middleware failure or capacity result. See `performance-testing.md` for the workload and limitations.
