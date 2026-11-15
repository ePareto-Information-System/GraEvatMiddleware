# Production Readiness Checklist

| Gate | Required evidence | Status |
| --- | --- | --- |
| C# implementation approval | Approved deviation from Python wording | External decision required |
| API contract | Joint approval by middleware and Odoo owners | External sign-off required |
| GRA contract | Production endpoints, credentials, certificate/auth and certification | External GRA evidence required |
| Functional implementation | 24 canonical operations and 12 GRA groups | Repository complete |
| Tenant isolation | Automated tests plus independent authenticated assessment | Local complete; external assessment required |
| Recovery/idempotency | Automated tests plus PostgreSQL concurrency run | Local complete; gated run required |
| Performance | Local baseline plus representative topology test | Collection ready; deployment evidence required |
| Security | Threat model, vulnerability scan and independent penetration test | Repository ready; external test required |
| Data protection | Classification plus approved retention/access/encryption | External legal/management decision required |
| Hosting | TLS, proxy, network, secret manager, DB and central monitoring | Architecture decision required |
| Backup/DR | Encrypted backup and successful isolated restore/DR exercise | Procedure ready; exercise required |
| Odoo E2E | Odoo to middleware to GRA and reconciliation evidence | Joint external test required |
| Pilot | Tenant onboarding, user/client acceptance and defect closure | External pilot required |
| Operations handover | On-call, escalation, maintenance and rollback authority | External sign-off required |

Production approval must not be inferred from repository test success alone.
