# ADR 0004 GRA Secret Indirection

**Status:** Accepted; production provider pending.

**Decision:** Persist only a tenant-specific secret name and resolve the GRA `security_key` at runtime from configuration or an approved secret store.

**Reason:** GRA credentials must not appear in tenant records, API responses, committed settings or logs.

**Consequences:** Production deployment must select a managed secret provider, access identity, rotation process and availability controls.
