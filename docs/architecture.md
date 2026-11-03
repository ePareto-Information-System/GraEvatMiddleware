# Architecture

The middleware uses five production projects with inward-facing dependencies:

```text
Api -> Application
Api -> Infrastructure
Api -> GraClient
Infrastructure -> Application -> Domain
GraClient -> Application -> Domain
```

`Domain` has no project dependencies. `Application` defines use cases and outbound interfaces. `Infrastructure` implements persistence, authentication, secrets and auditing. `GraClient` isolates the GRA v8.2 transport contract. `Api` is the HTTP host and composition root.

The Odoo-facing tenant credential and the tenant's GRA security key are separate trust boundaries. Tenant identity is resolved from the middleware credential rather than accepted from request data.

## Exchange logging

`ExchangeLoggingMiddleware` records the completed inbound HTTP exchange after correlation and exception handling have produced the final response. It observes authenticated tenant claims where available and accepts a transaction identifier from the invoice controller. Logging uses an independent database scope so a failed business request does not leave its logging operation attached to a failed EF Core change tracker.

`GraEvatClient` records outbound GRA exchanges using transaction, tenant, correlation and idempotency context passed explicitly in `GraInvoiceSubmission`. No global mutable context is used. A logging failure is isolated and never causes a tax request to be sent again or changes its result.

`SensitiveDataSanitizer` is shared by both paths. It redacts sensitive headers and recursively replaces sensitive JSON properties before applying the configured storage limit. Binary and unsupported content types are omitted. The `integration_exchange_logs` table is indexed by tenant/date, transaction, correlation ID, operation/date and outcome/date.

`IExchangeLogRetentionService` deletes records older than `ExchangeLogging:RetentionDays` when invoked by an authorized operational process. It is intentionally not exposed through a public endpoint or scheduled automatically in the development baseline.
