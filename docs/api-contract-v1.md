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
- `POST /api/v1/admin/tenants`
- `POST /api/v1/admin/tenants/{tenantId}/credentials`
- `POST /api/v1/admin/tenants/{tenantId}/credentials/{credentialId}/rotate`
- `DELETE /api/v1/admin/tenants/{tenantId}/credentials/{credentialId}`
- `POST /api/v1/admin/tenants/{tenantId}/activate`
- `POST /api/v1/admin/tenants/{tenantId}/suspend`
- `POST /api/v1/admin/tenants/{tenantId}/terminate`
- `PUT /api/v1/admin/tenants/{tenantId}/configuration`
- `POST /api/v1/invoices`
- `GET /api/v1/transactions/{transactionId}`
- `GET /api/v1/audit?limit=50`

Invoice submission requires an `Idempotency-Key`. Reusing a key with the same body returns the existing transaction. Reusing it with a different body returns HTTP 409.

The transaction resource may report `PendingGra` while the submission is being reconciled. Callers must use transaction lookup rather than resubmitting with a new idempotency key. `Succeeded` and `Rejected` are terminal for the current invoice slice. Retry and callback operations are internal middleware responsibilities and are not exposed as Odoo-facing endpoints.

Credential rotation atomically revokes the selected active key and returns its replacement once. Tenant termination is irreversible through the API and revokes every active credential. Audit lookup is always scoped to the tenant resolved from `X-API-Key`; the caller cannot supply another tenant identifier.

Problem responses include a stable `code` and `correlationId`. Current middleware codes include `AUTHENTICATION_FAILED`, `VALIDATION_FAILED`, `RESOURCE_NOT_FOUND`, `RESOURCE_CONFLICT`, `IDEMPOTENCY_CONFLICT`, `RATE_LIMIT_EXCEEDED`, `CONFIGURATION_MISSING` and `INTERNAL_ERROR`.

The machine-readable source of truth for this slice is `docs/openapi-v1.yaml`.

Refund, purchase, return, inventory, TIN validation and callback operations will be added after their canonical schemas and GRA mappings are approved.
