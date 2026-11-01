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
- `DELETE /api/v1/admin/tenants/{tenantId}/credentials/{credentialId}`
- `POST /api/v1/admin/tenants/{tenantId}/activate`
- `POST /api/v1/admin/tenants/{tenantId}/suspend`
- `POST /api/v1/invoices`
- `GET /api/v1/transactions/{transactionId}`

Invoice submission requires an `Idempotency-Key`. Reusing a key with the same body returns the existing transaction. Reusing it with a different body returns HTTP 409.

The machine-readable source of truth for this slice is `docs/openapi-v1.yaml`.

Refund, purchase, return, inventory, TIN validation and callback operations will be added after their canonical schemas and GRA mappings are approved.
