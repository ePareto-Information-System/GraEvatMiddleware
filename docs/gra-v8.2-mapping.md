# GRA E-VAT Version 8.2 Mapping

Status: Invoice staging mapping implemented; other operations remain draft

Source: https://documenter.getpostman.com/view/29809098/2sBXVeGCzK

The documented staging host is `vsdcstaging.vat-gh.com`. The implemented operations are:

- `POST /vsdc/api/v1/taxpayer/{reference}/invoice`
- `GET /vsdc/api/v1/taxpayer/{reference}/health`
- `POST /vsdc/api/v1/taxpayer/{reference}/invoice/callback`

All operations authenticate with the `security_key` request header. The taxpayer reference in the URL and the key must belong together.

The canonical invoice maps directly to the v8.2 invoice fields. Middleware supplies `flag` as `INVOICE`, calculates `totalExciseAmount` from the line items, supplies zero voucher amount for the current canonical contract, and supplies empty refund and purchase-order references. The GRA response is successful only when `response.status` is `SUCCESS`. The preferred external reference is `response.message.ysdcrecnum`, followed by signature and invoice number as fallbacks.

Standard-rated items use an empty `itemCategory`. The documented non-standard categories are `CST`, `TRSM`, `EXM`, `RNT` and `EXC_PLASTIC`. Levy C is zero for invoices dated on or after 1 January 2026.

The invoice callback body contains `reference`, `invoiceNumber` and `flag`. It is used before any retry to determine whether an uncertain invoice already reached GRA. Error E701 means the invoice was not found. Error E700 means the invoice has already been signed and requires callback reconciliation. E900 and E907 are credential failures. E990 is the documented request-rate limit and is treated as temporary.

The client preserves the documented GRA error code and also assigns an internal failure category. `E700` is classified as already processed and triggers reconciliation, `E701` as not found, `E900` and `E907` as authentication failures, and `E990` as rate limited. HTTP 5xx responses are temporary, malformed successful responses are ambiguous invalid responses, and other non-success responses are business rejections. Retry decisions use the category instead of depending only on individual string codes.

Refunds, refund cancellation, purchases, purchase returns, purchase-return cancellation, credit/debit notes, statements, TIN validation and inventory remain outside the implemented canonical slice until their Odoo-facing schemas are approved.

The taxpayer reference is GRA-facing tenant configuration. The GRA `security_key` is a secret and must never be included in this document, application settings committed to source control, logs or API responses.
