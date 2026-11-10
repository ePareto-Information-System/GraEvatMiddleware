# ADR 0002 PostgreSQL Persistence

**Status:** Accepted.

**Decision:** Use PostgreSQL with EF Core migrations for tenant, credential, transaction, audit, exchange and retry state.

**Reason:** Durable idempotency, transaction lookup, auditing and cross-instance retry leasing require shared transactional persistence.

**Consequences:** Deployment needs one controlled migration identity, least-privilege application access, encryption, backups, restore testing and capacity monitoring.
