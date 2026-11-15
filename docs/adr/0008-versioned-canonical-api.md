# ADR 0008 Versioned Canonical API

**Status:** Accepted.

**Decision:** Expose `/api/v1` independently of GRA transport details and maintain OpenAPI/Postman artifacts with it.

**Reason:** ERP clients should not change when an internal GRA path, authentication detail or response envelope changes.

**Consequences:** Breaking canonical changes require a reviewed version/contract decision; internal mapping changes should preserve v1 when possible.
