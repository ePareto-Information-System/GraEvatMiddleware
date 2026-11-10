# Canonical API Contract v1

Status: Implementation baseline

The canonical API is exposed under `/api/v1`. It is independent of the GRA transport contract so Odoo integrations do not need to know GRA-specific authentication, URLs or response structures.

## Authentication boundaries

- Administrative tenant and credential operations require `X-Admin-Key`.
- Tenant operations require a middleware-issued `X-API-Key`.
- Tenant identity is resolved from `X-API-Key`; callers cannot select a tenant in request data.
- GRA taxpayer references and security keys are never exposed through this API.

## Initial operations

- `GET /api/v1/health/live`
- `GET /api/v1/health/ready`
- `GET /api/v1/admin/operations/status`
- `POST /api/v1/admin/tenants`
- `POST /api/v1/admin/tenants/{tenantId}/credentials`
- `POST /api/v1/admin/tenants/{tenantId}/credentials/{credentialId}/rotate`
- `DELETE /api/v1/admin/tenants/{tenantId}/credentials/{credentialId}`
- `POST /api/v1/admin/tenants/{tenantId}/activate`
- `POST /api/v1/admin/tenants/{tenantId}/suspend`
- `POST /api/v1/admin/tenants/{tenantId}/terminate`
- `PUT /api/v1/admin/tenants/{tenantId}/configuration`
- `POST /api/v1/invoices`
- `POST /api/v1/refunds`
- `POST /api/v1/purchases`
- `POST /api/v1/purchase-returns`
- `POST /api/v1/refund-cancellations`
- `POST /api/v1/purchase-return-cancellations`
- `GET /api/v1/transactions/{transactionId}`
- `GET /api/v1/audit?limit=50`

Every fiscal submission requires an `Idempotency-Key`. Reusing a key for the same operation and body returns the existing transaction. Reusing it for the same operation with a different body returns HTTP 409. Keys are isolated by tenant and operation.

Refunds require the original GRA `reference` and a `refundType` of `FULL` or `PARTIAL`. Purchase returns require the original purchase reference. Refund and purchase-return cancellation bodies contain the cancellation invoice number, original reference, transaction timestamp and positive total amount. The middleware selects the exact GRA flag; callers cannot supply or override it.

The transaction resource may report `PendingGra` while the submission is being reconciled. Callers must use transaction lookup rather than resubmitting with a new idempotency key. `Succeeded` and `Rejected` are terminal for the current invoice slice. Retry and callback operations are internal middleware responsibilities and are not exposed as Odoo-facing endpoints.

Credential rotation atomically revokes the selected active key and returns its replacement once. Tenant termination is irreversible through the API and revokes every active credential. Audit lookup is always scoped to the tenant resolved from `X-API-Key`; the caller cannot supply another tenant identifier.

Problem responses include a stable `code` and `correlationId`. Current middleware codes include `AUTHENTICATION_FAILED`, `VALIDATION_FAILED`, `RESOURCE_NOT_FOUND`, `RESOURCE_CONFLICT`, `IDEMPOTENCY_CONFLICT`, `RATE_LIMIT_EXCEEDED`, `CONFIGURATION_MISSING` and `INTERNAL_ERROR`.

Liveness confirms that the middleware process can serve HTTP requests. Readiness separately verifies PostgreSQL connectivity. When GRA integration is enabled, readiness also requires an active tenant and a successful GRA health response. It returns HTTP 503 without exposing tenant references or secret names when an enabled dependency is unavailable.

The administrative operations status endpoint reports aggregate request counts and duration, pending transaction counts, retry-queue states, exhausted jobs and expired leases. It requires `X-Admin-Key` and never returns tenant identifiers, invoice payloads or credentials.

The machine-readable source of truth for this slice is `docs/openapi-v1.yaml`.

Credit/debit note, statement, inventory and TIN-validation operations remain for later phases. GRA callback handling is an internal recovery control and is not exposed to callers.
