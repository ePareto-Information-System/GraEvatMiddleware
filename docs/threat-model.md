# Threat Model

## Scope

This initial threat model covers the canonical API, tenant administration, PostgreSQL persistence, GRA staging adapter, retry worker and operational secrets. It must be reviewed again when production hosting, the production GRA host and the managed secret store are selected.

## Assets and trust boundaries

- Middleware tenant API keys and the administrative key
- GRA security keys referenced through secret-store names
- Invoice payloads, transaction status, retry payloads, audit events and exchange logs
- Client-to-middleware, middleware-to-PostgreSQL and middleware-to-GRA boundaries
- Administrative and operational access boundary

## Principal threats and controls

| Threat | Existing control | Remaining production action |
| --- | --- | --- |
| Stolen tenant API key | HMAC-hashed storage, rotation, revocation, suspension, termination and rate limiting | Store client keys securely and define compromise response time |
| Cross-tenant data access | Tenant identity comes from the key; transaction and audit queries include tenant ID | Perform authenticated penetration and isolation testing |
| GRA key disclosure | Secret indirection, redaction, host allowlist, HTTPS and redirects disabled | Use a managed secret store and scheduled rotation |
| Duplicate invoice after timeout | Tenant-scoped idempotency, callback-first reconciliation and no ambiguous automatic resubmission | Confirm production callback semantics with GRA |
| Multiple workers process one retry | Conditional database claim and expiring lease | Validate with PostgreSQL concurrency/load tests |
| Log or error leakage | Recursive redaction, body limits, stable problem responses and no raw keys in audit events | Restrict support access and approve retention periods |
| Request flooding | Credential/IP-partitioned fixed-window limiting | Tune limits behind the production proxy and add perimeter controls |
| Database compromise | Least-privilege application role and minimized responses | Enable encryption at rest, backups, monitoring and credential rotation |
| Unauthorized configuration change | Separate admin key and audit events | Replace the shared admin key with managed operator identity if required |

## Security gates before production

1. Approve the C# implementation deviation from the original Python wording.
2. Complete authenticated API and infrastructure penetration testing.
3. Remediate every critical and high finding.
4. Approve hosting, TLS termination, database encryption, secret management and retention.
5. Confirm production GRA endpoints, credentials, certification and callback behaviour.
6. Exercise credential compromise, restore and incident-response procedures.
