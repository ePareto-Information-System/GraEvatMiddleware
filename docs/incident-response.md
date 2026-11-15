# Incident Response Playbook

## Common process

1. **Detect and declare:** Assign incident lead, severity and UTC timeline.
2. **Contain:** Disable affected credentials, tenant, GRA integration or traffic path without deleting evidence.
3. **Assess:** Identify tenant scope, data/credential exposure and uncertain fiscal transactions.
4. **Eradicate:** Rotate material, patch defects and remove unauthorized access.
5. **Recover:** Verify readiness, reconcile pending transactions and restore traffic by approval.
6. **Review:** Record evidence, notifications, root cause, remediation owners/dates and lessons.

Do not place raw keys, passwords, connection strings, taxpayer payloads or unnecessary personal information in tickets or chat.

## Tenant API-key compromise

Suspend the tenant for broad containment or revoke the individual credential. Inspect tenant-scoped audit/exchange activity, issue a replacement through a secure channel, confirm the old key fails and reconcile suspicious transactions.

## Administrative-key compromise

Restrict administrative access, replace the key in the secret platform and every authorized operator location, restart/reload safely, inspect tenant lifecycle/configuration audit events and consider all issued credentials/configuration touched during the exposure window.

## GRA-key compromise

Disable GRA integration, rotate through the approved GRA process, update only the secret-store value, validate health in the approved environment and reconcile pending/uncertain transactions before re-enabling.

## Database or backup compromise

Restrict database access, preserve forensic evidence, rotate database/admin/GRA/API material according to exposure, assess confidential taxpayer data, invoke legal/privacy notification decisions and restore only from trusted verified backups.

## Stuck retries or growing pending queue

Disable the retry worker if behaviour is unsafe, inspect exhausted/expired jobs and GRA availability, do not reset attempt counts or resubmit manually, resolve the cause and resume under an approved reconciliation plan.

## Severity and escalation

Critical conditions include confirmed credential/data compromise, cross-tenant access, duplicate fiscal documents or broad service unavailability. Escalate immediately to management, security, operations, the affected business owner and GRA/legal contacts as applicable. Contact details belong in the organization-controlled incident directory, not this public repository.
