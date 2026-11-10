# API Consumer Guide

## Authentication

Tenant operations use `X-API-Key`. Administrative keys and GRA `security_key` values must never be placed in ERP code. Obtain the tenant key through the approved onboarding channel and store it as a client secret.

## State-changing calls

Send an `Idempotency-Key` of 8-128 characters for invoices, refunds, purchases, returns, cancellations, notes, statements and inventory. Generate one key per business action and retain it with the client record. If a request times out, reuse the same key and identical body or query transaction status; do not generate a new key.

First accepted submission returns HTTP 201. An identical replay returns HTTP 200 and the original transaction. The same key with different content returns HTTP 409.

## Status and errors

`PendingGra` is not failure and not permission to resubmit. Query `GET /api/v1/transactions/{transactionId}`. Problem responses use stable codes such as `AUTHENTICATION_FAILED`, `VALIDATION_FAILED`, `IDEMPOTENCY_CONFLICT`, `RATE_LIMIT_EXCEEDED` and `INTERNAL_ERROR`, plus a correlation ID for support.

Handle HTTP 401 by stopping and correcting credentials; 422 by correcting the request; 409 by retrieving/reconciling the original action; 429 by waiting according to the client policy; and 5xx/timeout by retaining the same idempotency key and checking status.

## Identification

TIN and Ghana Card validation are read-only and do not need idempotency keys. A normalized result contains `isValid`, identity fields and an optional error code. Do not retain more personal information than the approved business purpose requires.

## Contract and examples

Use Swagger/OpenAPI and the `GRA eVAT API Contract` Postman collection for complete schemas and examples. The canonical API deliberately hides GRA URLs, flags and credentials.
