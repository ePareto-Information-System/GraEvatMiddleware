# ADR 0007 Sanitized Exchange Logging

**Status:** Accepted.

**Decision:** Record tenant-aware inbound/outbound exchange metadata and optional bodies after recursive redaction, size limits and identification-path masking.

**Reason:** Support and audit need correlation evidence without turning diagnostics into a credential repository.

**Consequences:** Logging failures never alter fiscal results. Access, encryption and retention still require production policy.
