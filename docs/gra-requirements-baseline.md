# GRA eVAT Requirements Baseline

This baseline traces the middleware implementation to the GRA eVAT v8.2 Postman documentation and the approved system requirements. The GRA documentation is the authority for upstream paths, field names and flags; the middleware exposes a separate, stable API to calling applications.

| Capability | Canonical middleware operation | GRA operation | Control |
| --- | --- | --- | --- |
| Invoice | `POST /api/v1/invoices` | `/invoice`, `INVOICE` | Idempotency, persistence, callback-first retry |
| Refund | `POST /api/v1/refunds` | `/invoice`, `REFUND` or `PARTIAL_REFUND` | Original reference required |
| Refund cancellation | `POST /api/v1/refund-cancellations` | `/cancellation`, `REFUND_CANCELATION` | Idempotency and audit |
| Purchase | `POST /api/v1/purchases` | `/invoice`, `PURCHASE` | Tenant isolation and audit |
| Purchase return | `POST /api/v1/purchase-returns` | `/invoice`, `PURCHASE_RETURN` | Original reference required |
| Return cancellation | `POST /api/v1/purchase-return-cancellations` | `/cancellation`, `PURCHASE_RETURN_CANCELATION` | Idempotency and audit |
| Credit/debit note | `POST /api/v1/credit-debit-notes` | `/note`, `CREDIT_NOTE` or `DEBIT_NOTE` | Middleware owns flag selection |
| Statement | `POST /api/v1/statements-of-account` | `/statment_of_account` | Published path spelling preserved |
| Identification | two `GET /api/v1/identifications/...` operations | `/identification/tin/...` and `/identification/nationalId/...` | Read-only, authenticated, normalized response |
| Inventory | `POST /api/v1/inventory` | `/inventory` | Idempotency, persistence and audit |
| Health | internal readiness | `/health` | Environment and dependency readiness |
| Callback | internal recovery | `/invoice/callback` | Prevents duplicate invoice resubmission |

All GRA calls use the tenant's configured taxpayer reference and a `security_key` retrieved through secret indirection. The client permits only HTTPS, the configured allowlist and the approved staging host while the environment is Staging. Secrets are redacted from request/response records.

The published contract does not provide reconciliation endpoints for notes, statements or inventory. An ambiguous timeout is therefore retained as `PendingGra`; the middleware does not risk a blind resubmission.
