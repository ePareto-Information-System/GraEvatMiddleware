# Security Assurance Checklist

## Implemented and locally verifiable controls

| Area | Control and repository evidence | Verification |
| --- | --- | --- |
| Tenant authentication | One-time API keys; keyed HMAC-SHA256 hashes; pepper outside database | Credential issue/rotation/revocation tests |
| Administrative boundary | Separate `X-Admin-Key`; admin operations never accept a tenant key | Admin authorization tests |
| Tenant isolation | Tenant ID derived from credential; scoped transaction/audit/configuration queries | Cross-tenant and suspended-tenant tests |
| GRA credentials | Secret name persisted; key resolved at runtime and sent only as redacted `security_key` | Client and sanitizer tests |
| Transport | GRA requires HTTPS, Staging environment and allowlisted approved host; redirects disabled | Configuration/client tests |
| Input handling | Canonical model validation, bounded keys/identifiers and required business dates | Validation tests |
| Duplicate prevention | Tenant/operation idempotency, request hash, database uniqueness and callback-first recovery | Replay/conflict/retry tests |
| Sensitive diagnostics | Header/body redaction, URL identifier masking, body limits and stable problem responses | Sanitizer and exchange-log tests |
| Abuse protection | Credential/IP-partitioned fixed-window limiter | Rate-limit integration tests |
| Database access | Separate least-privilege application role; parameterized EF Core access | Configuration/runbook review |
| Dependency risk | Locked NuGet dependencies and vulnerability scan | `dotnet list ... --vulnerable --include-transitive` |
| Audit/monitoring | Tenant-aware audit, sanitized exchanges, correlation IDs and protected aggregate status | Integration/operations tests |

## Production controls requiring an approved environment

- TLS termination and certificates for client-to-middleware traffic.
- Managed operator identity or approved handling/rotation of the shared admin key.
- Managed secret store, workload identity and GRA-key rotation schedule.
- PostgreSQL network isolation, encryption at rest, encrypted backups and monitored privileged access.
- Central security logging, alert routing, time synchronization and tamper-resistant retention.
- Perimeter denial-of-service protection and aggregate rate limits across multiple instances.
- Patch ownership, maintenance window and emergency vulnerability procedure.

## Penetration-test scope

An independent test must cover unauthenticated and authenticated API behaviour, credential lifecycle, object/tenant authorization, identifier enumeration, injection/malformed JSON, request smuggling and proxy handling, rate-limit bypass, secret/log/error leakage, GRA-host/redirect controls, PostgreSQL privilege boundaries and deployment misconfiguration.

Acceptance requires:

1. No unresolved critical or high finding.
2. Medium findings have approved remediation or risk treatment with owners/dates.
3. Tenant-A credentials cannot access, infer or mutate Tenant-B data/configuration.
4. No admin, tenant or GRA credential appears in responses, logs, traces or committed artifacts.
5. Retest evidence confirms critical/high remediation.

The repository does not claim that this external assessment has occurred.

## Credential-compromise exercise criteria

Record detection time, affected credential, tenant scope, containment action, revocation/rotation time, evidence locations, pending-transaction reconciliation, stakeholder notification decision, recovery approval and lessons. Never paste raw secrets into the exercise record.
