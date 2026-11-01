# Canonical API Contract v1

Status: Draft

The canonical API is versioned under `/api/v1`. Initial implementation will cover:

- `GET /api/v1/health/live`
- invoice submission with an `Idempotency-Key` header;
- transaction status lookup;
- tenant API-key authentication;
- stable success and error envelopes.

Refund, purchase, return, inventory, TIN validation and callback endpoints will be added after their canonical schemas and GRA mappings are approved.
