# Data Classification and Retention

| Data | Classification | Storage/control | Retention action |
| --- | --- | --- | --- |
| Admin key, tenant API keys, API-key pepper, GRA `security_key` | Secret | User secrets or approved secret manager; never plaintext database/log storage | Rotate under the security policy; revoke immediately on compromise |
| Taxpayer references, TIN/Ghana Card inputs, partner identity and addresses | Confidential taxpayer data | TLS, tenant isolation, least-privilege database access | Retention period requires business and legal approval |
| Fiscal payloads, transaction and retry data | Confidential financial/tax data | PostgreSQL, access control, backup protection and encryption requirements | Retain only for the approved fiscal/audit period |
| Audit events and exchange metadata | Restricted operational/audit data | Immutable application events and role-restricted queries | Apply the approved audit retention period |
| Sanitized exchange bodies | Restricted diagnostic data | Recursive redaction and configurable capture/size limits | Automated cleanup uses `ExchangeLogging:RetentionDays` |

The current default exchange-log retention is configurable and is not a legal-policy decision. Management must approve retention periods, database/backup encryption, access roles, deletion holds and restoration procedures before production. Identification responses are logged only through the same sanitization and retention controls.
