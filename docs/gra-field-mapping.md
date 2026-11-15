# Canonical to GRA Field Mapping

This document is the developer mapping specification for the implemented GRA eVAT v8.2 operations. The middleware supplies `userName` as `middleware`, resolves `{reference}` from tenant configuration and adds `security_key` from secret storage. Callers cannot override those values.

## Shared transport controls

| Concern | Middleware behaviour |
| --- | --- |
| Base URL | HTTPS, `Staging` environment and explicit host allowlist required |
| Redirects | Disabled so `security_key` is not forwarded to another host |
| Authentication | `security_key` header resolved from the tenant secret name |
| Correlation | Middleware correlation ID is retained in internal exchange evidence |
| Success | Valid documented success envelope, or empty body where the GRA operation documents no response body |
| Errors | GRA codes are normalized to `E###` and assigned authentication, business, temporary, rate-limit, already-processed, not-found or invalid-response categories |
| Ambiguity | Invoice-family operations reconcile by callback; note/statement/inventory remain pending |

## Invoice family

**GRA path:** `POST /vsdc/api/v1/taxpayer/{reference}/invoice`

| Canonical field | GRA field | Rule |
| --- | --- | --- |
| `invoiceNumber` | `invoiceNumber` | Required document identifier |
| `currency` | `currency` | Canonical currency code |
| `exchangeRate` | `exchangeRate` | Positive |
| operation | `flag` | Middleware selects `INVOICE`, `REFUND`, `PARTIAL_REFUND`, `PURCHASE` or `PURCHASE_RETURN` |
| `calculationType` | `calculationType` | `INCLUSIVE` or `EXCLUSIVE` |
| `transactionDate` | `transactionDate` | `yyyy-MM-dd` |
| `totalVat` | `totalVat` | Non-negative |
| `totalLevy` | `totalLevy` | Non-negative |
| `totalAmount` | `totalAmount` | Non-negative canonical total |
| item excise sum | `totalExciseAmount` | Calculated by middleware |
| fixed value | `voucherAmount` | `0` in v1 |
| partner fields | `businessPartnerName`, `businessPartnerTin` | Required |
| `saleType` | `saleType` | `NORMAL` or `EXPORT` |
| `discountType`, `discountAmount` | same | Optional type and non-negative value |
| fixed value | `taxType` | `STANDARD` |
| original document reference | `reference` | Required for refunds/returns; empty otherwise |
| `groupReferenceId` | `groupReferenceId` | Empty when absent |
| fixed value | `purchaseOrderReference` | Empty in v1 |
| `items[]` | `items[]` | Field mapping below |

| Canonical item | GRA item | Rule |
| --- | --- | --- |
| `itemCode` | `itemCode` | Required |
| `itemCategory` | `itemCategory` | Empty for standard; documented categories otherwise |
| `expireDate` | `expireDate` | `yyyy-MM-dd` or empty |
| `description` | `description` | Required |
| `quantity`, `unitPrice` | same | Positive quantity, non-negative price |
| `levyAmountA` to `levyAmountE` | same | Non-negative |
| `discountAmount`, `exciseAmount` | same | Non-negative |
| `batchCode` | `batchCode` | Empty when absent |

**Response:** success requires `response.status=SUCCESS`; preferred external reference is `ysdcrecnum`, then `ysdcregsig`, then `num`.

**Recovery:** callback-first. E700 means already processed; E701 means callback not found; E900/E907 are authentication; E990 is rate limit.

## Refund and purchase-return cancellation

**GRA path:** `POST /vsdc/api/v1/taxpayer/{reference}/cancellation`

| Canonical field | GRA field | Rule |
| --- | --- | --- |
| `invoiceNumber` | `invoiceNumber` | Cancellation document number |
| `reference` | `reference` | Original GRA reference |
| operation | `flag` | `REFUND_CANCELATION` or `PURCHASE_RETURN_CANCELATION` |
| `transactionDate` | `transactionDate` | ISO 8601 |
| `totalAmount` | `totalAmount` | Positive |

The documented success may have no body. Ambiguous cancellation is retained pending; it is not blindly resubmitted.

## Credit and debit note

**Canonical endpoint:** `POST /api/v1/credit-debit-notes`
**GRA path:** `POST /vsdc/api/v1/taxpayer/{reference}/note`

| Canonical field | GRA field | Rule |
| --- | --- | --- |
| `noteNumber` | `noteNumber` | Transaction document number |
| `noteType` | `flag` | Middleware maps `CREDIT` to `CREDIT_NOTE`, `DEBIT` to `DEBIT_NOTE` |
| `currency`, `exchangeRate`, `noteAmount` | same | Validated canonical values |
| `calculationType` | `calculationType` | Inclusive/exclusive |
| partner fields | `businessPartnerName`, `businessPartnerTin` | Required |
| `groupReferenceId` | `groupReferenceId` | Required |
| `transactionDate` | `transactionDate` | `yyyy-MM-dd` |
| `remarks` | `remarks` | Empty when absent |
| `noteLineType` | `noteLineFlag` | `INVOICE` or `PURCHASE` |
| `noteLines[]` | `noteLines[]` | `invoiceNumber`, `invoiceDate` (`yyyy-MM-dd`), `invoiceAmount` |

Known business codes include E722 existing note, E723 duplicate invoice number and E724 excessive credit amount. No callback is published, so ambiguous delivery stays pending.

## Statement of account

**Canonical endpoint:** `POST /api/v1/statements-of-account`
**GRA path:** `POST /vsdc/api/v1/taxpayer/{reference}/statment_of_account`

The published path spelling is preserved deliberately.

| Canonical field | GRA field | Rule |
| --- | --- | --- |
| `currency`, `exchangeRate` | same | Required |
| `totalVat`, `totalAmount`, `totalLevies` | same | Non-negative |
| partner fields | `businessPartnerName`, `businessPartnerTin` | Required |
| `groupReferenceId` | `groupReferenceId` | Transaction document number |
| `transactionDate` | `transactionDate` | ISO 8601 |
| `calculationType` | `calculationType` | Inclusive/exclusive |
| `lines[]` | `groupInvoiceLines[]` | Mapped below |

Statement lines map currency, exchange rate, calculation type, invoice number, optional reference, flag, invoice VAT, amount, levies and ISO transaction date. Allowed flags are `INVOICE`, `REFUND`, `PARTIAL_REFUND`, `CREDIT_NOTE`, `DEBIT_NOTE` and `REFUND_CANCELATION`. E703 identifies an invalid item count and E881 a missing group reference. No callback is published.

## Inventory

**Canonical endpoint:** `POST /api/v1/inventory`
**GRA path:** `POST /vsdc/api/v1/taxpayer/{reference}/inventory`

| Canonical field | GRA field | Rule |
| --- | --- | --- |
| `inventoryDate` | `inventoryDate` | `yyyy-MM-dd` |
| `inventories[].code` | `inventories[].code` | Required |
| `description`, `quantity`, `unitPrice` | same | Validated values |
| `batchCode` | `batchCode` | Empty when absent |
| `expiryDate` | `expiryDate` | `yyyy-MM-dd` or empty |

Top-level `{ "status": "SUCCESS" }` is accepted. The local document number is `INVENTORY-yyyyMMdd`. No callback is published.

## TIN and Ghana Card validation

| Canonical endpoint | GRA method/path | Response mapping |
| --- | --- | --- |
| `GET /api/v1/identifications/tin/{tin}` | `GET .../identification/tin/{value}` | `data.tin`, `type`, `name`, `sector`, `address` |
| `GET /api/v1/identifications/ghana-card/{nationalId}` | `GET .../identification/nationalId/{value}` | Same normalized fields |

The operations are read-only and need no idempotency key or transaction row. Identifier values are masked in diagnostic URLs. The Ghana Card page's generated cURL repeats the TIN path; production certification must confirm the displayed `nationalId` path.

## Health and callback

| Operation | GRA path | Purpose |
| --- | --- | --- |
| Health | `GET .../health` | Readiness when GRA integration is enabled; expects `status=UP` |
| Invoice callback | `POST .../invoice/callback` | Reconcile invoice number, reference and flag before any eligible retry |

## Canonical idempotency and error behaviour

Every state-changing canonical operation requires an 8-128 character `Idempotency-Key`. The scope is tenant plus operation. The same key/body returns the original transaction; a changed body returns HTTP 409. Canonical validation returns HTTP 422. Authentication failures return 401, rate limiting returns 429, and unexpected failures return a stable RFC 7807 response with a correlation ID.
