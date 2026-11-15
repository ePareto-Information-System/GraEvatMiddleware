# Support Troubleshooting Guide

## Triage order

1. Obtain UTC time, correlation ID, transaction ID, tenant identity and endpoint; never request raw keys.
2. Check liveness and readiness.
3. Check the tenant status/credential lifecycle and transaction status.
4. Inspect tenant-authorized audit and restricted sanitized exchange records.
5. Inspect aggregate pending/retry/exhausted/expired signals.
6. Classify configuration, authentication, validation, business rejection, temporary, ambiguous or internal failure.

## Common outcomes

| Symptom/code | Meaning | Safe action |
| --- | --- | --- |
| 401 | Missing/invalid/revoked/suspended credential | Verify tenant/key lifecycle; rotate if necessary |
| 422 | Canonical validation failed | Correct fields; reuse business key only for the corrected intended action according to conflict outcome |
| 409 idempotency conflict | Key reused with changed body | Retrieve original action; do not bypass with arbitrary key |
| 429 | Middleware or GRA rate limit | Back off and inspect configured limits |
| `GRA_INTEGRATION_DISABLED` | Safety switch is off | Enable only after approved configuration review |
| `GRA_SECRET_NOT_CONFIGURED` | Tenant secret name cannot resolve | Correct secret-store entry without exposing value |
| `PendingGra` | Outcome uncertain or integration disabled | Query/reconcile; never blindly resubmit |
| exhausted retry | Automated recovery reached limit | Investigate GRA/callback and follow incident escalation |
| readiness 503 | Database or enabled GRA dependency unavailable | Remove from traffic and restore dependency |

For E700 use callback reconciliation; E701 means callback not found; E900/E907 indicate GRA credentials; E990 indicates rate limit. Notes/statements/inventory have no published callback and remain pending after ambiguity.

## Escalation evidence

Provide sanitized timestamps, correlation/transaction IDs, HTTP/GRA code, tenant status, retry state, deployment commit and actions already taken. Do not include payload bodies unless authorized and necessary.
