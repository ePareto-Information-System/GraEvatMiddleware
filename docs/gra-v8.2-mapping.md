# GRA E-VAT Version 8.2 Mapping

Status: all 12 documented GRA operation groups implemented

Source: https://documenter.getpostman.com/view/29809098/2sBXVeGCzK

The documented staging host is `vsdcstaging.vat-gh.com`. The implemented operations are:

- `POST /vsdc/api/v1/taxpayer/{reference}/invoice`
- `GET /vsdc/api/v1/taxpayer/{reference}/health`
- `POST /vsdc/api/v1/taxpayer/{reference}/invoice/callback`
- `POST /vsdc/api/v1/taxpayer/{reference}/cancellation`
- `POST /vsdc/api/v1/taxpayer/{reference}/note`
- `POST /vsdc/api/v1/taxpayer/{reference}/statment_of_account`
- `GET /vsdc/api/v1/taxpayer/{reference}/identification/tin/{value}`
- `GET /vsdc/api/v1/taxpayer/{reference}/identification/nationalId/{value}`
- `POST /vsdc/api/v1/taxpayer/{reference}/inventory`

| GRA v8.2 operation group | Middleware status |
| --- | --- |
| Invoice | Implemented |
| Invoice refunds | Implemented (full and partial) |
| Refund cancellation | Implemented |
| Purchase | Implemented |
| Purchase return | Implemented |
| Purchase return cancellation | Implemented |
| Credit note / debit note | Implemented |
| Statement of account | Implemented |
| Health check | Implemented |
| TIN and Ghana Card validator | Implemented |
| Inventory | Implemented |
| Invoice callback | Implemented |

This GRA-facing count is separate from the canonical middleware API, which now exposes 24 operations to ERP clients and administrators. The external GRA transport and `security_key` remain hidden from callers.

All operations authenticate with the `security_key` request header. The taxpayer reference in the URL and the key must belong together.

The canonical invoice maps directly to the v8.2 invoice fields. Middleware supplies `flag` as `INVOICE`, calculates `totalExciseAmount` from the line items, supplies zero voucher amount for the current canonical contract, and supplies empty refund and purchase-order references. The GRA response is successful only when `response.status` is `SUCCESS`. The preferred external reference is `response.message.ysdcrecnum`, followed by signature and invoice number as fallbacks.

Standard-rated items use an empty `itemCategory`. The documented non-standard categories are `CST`, `TRSM`, `EXM`, `RNT` and `EXC_PLASTIC`. Levy C is zero for invoices dated on or after 1 January 2026.

The invoice callback body contains `reference`, `invoiceNumber` and `flag`. It is used before any retry to determine whether an uncertain invoice already reached GRA. Error E701 means the invoice was not found. Error E700 means the invoice has already been signed and requires callback reconciliation. E900 and E907 are credential failures. E990 is the documented request-rate limit and is treated as temporary.

The client preserves the documented GRA error code and also assigns an internal failure category. `E700` is classified as already processed and triggers reconciliation, `E701` as not found, `E900` and `E907` as authentication failures, and `E990` as rate limited. HTTP 5xx responses are temporary, malformed successful responses are ambiguous invalid responses, and other non-success responses are business rejections. Retry decisions use the category instead of depending only on individual string codes.

The shared invoice transport uses `REFUND`, `PARTIAL_REFUND`, `PURCHASE` and `PURCHASE_RETURN` flags. Refunds and purchase returns carry the original document reference. Cancellation uses the documented `/cancellation` transport and its exact published flags, `REFUND_CANCELATION` and `PURCHASE_RETURN_CANCELATION`.

Credit/debit notes use `CREDIT_NOTE` or `DEBIT_NOTE`; the caller selects `CREDIT` or `DEBIT` and cannot inject a GRA flag. Statements use GRA's published `statment_of_account` path spelling and map canonical `lines` to `groupInvoiceLines`. Identification validation is read-only and therefore does not require idempotency. Inventory, notes and statements use tenant-scoped idempotency and durable transaction/audit records. They are not automatically retried because GRA publishes no authoritative callback for reconciling an uncertain result.

The taxpayer reference is GRA-facing tenant configuration. The GRA `security_key` is a secret and must never be included in this document, application settings committed to source control, logs or API responses.
