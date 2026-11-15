# Administrator Guide

## Responsibilities

Administrators provision tenants, manage credentials/configuration, control staging/retry switches, monitor readiness and queues, coordinate incidents and retain evidence. Administrators do not copy GRA keys into database fields, logs or tickets.

## Tenant lifecycle

1. Verify approved tenant identity, taxpayer reference, environment and secure key-delivery channel.
2. Store the GRA key under a unique secret name.
3. Register the tenant and deliver the returned API key once.
4. Verify invalid-key rejection, valid authentication and status lookup.
5. Use rotation for planned/compromise replacement, revocation for one key, suspension for reversible tenant-wide stop and termination only for irreversible closure.

## Operational controls

Keep GRA and retry workers disabled until configuration is reviewed. Enable retry scheduling before worker processing. Check liveness, readiness and protected operations status after deployment. Treat exhausted jobs, expired leases and growing pending transactions as alerts requiring investigation.

## Safe support

Use correlation ID, transaction ID and tenant-approved identifiers. Restrict access to audit/exchange data. Redaction reduces risk but does not make fiscal/identity data public. Follow incident and recovery playbooks for compromise/outage.

## Production prerequisites

Do not authorize production until hosting/TLS, secret management, database encryption/backups, retention, monitoring, independent penetration testing, GRA certification, Odoo E2E and pilot acceptance are approved and evidenced.
