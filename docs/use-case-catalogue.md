# Use-case Catalogue

## Tenant administration

An administrator registers and configures each taxpayer independently, activates or suspends access, and issues, rotates or revokes tenant API credentials. Plaintext API keys are returned only at issuance; stored credentials are keyed hashes protected by the API-key pepper.

## Fiscal submissions

A calling application submits invoices, refunds, purchases, returns, cancellations, notes, statements or inventory with its tenant API key and an operation-specific idempotency key. The middleware validates the canonical request, derives the tenant and GRA flags, persists the transaction, calls GRA and records a sanitized audit trail. Replaying the same key and body returns the original result; changing the body produces a conflict.

## Identification validation

A tenant validates a TIN or Ghana Card identifier through a read-only endpoint. The middleware calls the applicable GRA identification route and returns a normalized result containing the taxpayer identity fields or an error code. No fiscal transaction is created.

## Recovery and support

For invoice-family submissions with uncertain outcomes, a durable worker checks the GRA callback before any eligible retry. Operators use correlation IDs, tenant-scoped transaction lookup, audit events, sanitized exchange logs, readiness and aggregate status to diagnose failures without exposing credentials.

## Safety boundaries

GRA integration is disabled by default. Real staging calls require explicit configuration, approved tenant credentials and host safeguards. Production endpoints are not enabled by the current configuration, and Odoo-specific workflows remain the responsibility of the calling application integration.
