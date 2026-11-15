# Middleware Use-case Catalogue

This catalogue defines middleware-owned success and failure behaviour. Odoo-specific models, user interfaces and workflows remain outside this repository.

## UC-01 New tenant onboarding

- **Actor:** Authorized administrator.
- **Preconditions:** Approved organisation, taxpayer reference, GRA environment, secret name and secure delivery channel.
- **Trigger and flow:** Store the GRA key outside the application, register the tenant, capture the one-time middleware key, deliver it securely and verify authentication/readiness.
- **Failure flow:** Duplicate identity returns conflict; invalid data returns validation error; missing admin key returns unauthorized.
- **Expected result and component:** An isolated active tenant and credential managed by tenant administration/authentication, with no plaintext key in storage or logs.
- **Acceptance and evidence:** Another tenant cannot use the key or access its records; tenant administration and authentication tests plus the onboarding runbook.

## UC-02 API credential lifecycle

- **Actor:** Authorized administrator.
- **Preconditions:** Existing non-terminated tenant.
- **Trigger and flow:** Issue, rotate or revoke a key, or suspend the tenant; apply the action atomically, audit it and return new key material only once.
- **Failure flow:** Unknown key returns not found; terminated tenant rejects issuance; old/revoked keys fail immediately.
- **Expected result and component:** Only active credentials authenticate through tenant management and the API-key authenticator.
- **Acceptance and evidence:** Rotation revokes exactly the selected key and creates one replacement; lifecycle unit/integration tests.

## UC-03 Successful fiscal transaction

- **Actor:** Authenticated ERP/API client.
- **Preconditions:** Active tenant, valid payload, unique idempotency key and approved GRA configuration.
- **Trigger and flow:** Authenticate, validate, persist, map, submit to GRA, validate the response, save the reference/status and return the canonical result.
- **Failure flow:** Configuration-disabled mode records pending without an outbound request.
- **Expected result and component:** One tenant-owned transaction with sanitized evidence through controllers, operation services and the GRA adapter.
- **Acceptance and evidence:** Correct GRA path/flag/payload, HTTP 201 initially and 200 on replay; workflow and mapping tests. Real staging evidence is gated.

## UC-04 Invalid or incomplete request

- **Actor:** Authenticated ERP/API client.
- **Preconditions:** Valid tenant credential.
- **Trigger and flow:** Submit missing, malformed, out-of-range or unsupported fields; reject before GRA submission.
- **Failure flow:** Missing/invalid idempotency key is also a validation failure.
- **Expected result and component:** HTTP 422 `VALIDATION_FAILED` with correlation ID from API validation.
- **Acceptance and evidence:** Required dates, identifiers, amounts, flags and lists are checked; validation integration tests.

## UC-05 Invalid tenant or API credential

- **Actor:** Unauthenticated or incorrectly authenticated caller.
- **Preconditions:** Missing, malformed, revoked, rotated, suspended or cross-tenant key.
- **Trigger and flow:** Call a protected operation; authentication stops execution before the controller.
- **Failure flow:** Rate limiting may reject abusive attempts without revealing key validity.
- **Expected result and component:** HTTP 401 with no tenant data or secret detail from the authentication handler.
- **Acceptance and evidence:** No protected action occurs; missing-key, revoked-key and suspension/termination tests.

## UC-06 GRA authentication or authorization failure

- **Actor:** Middleware GRA adapter.
- **Preconditions:** GRA integration enabled with an unauthorized external credential.
- **Trigger and flow:** GRA returns HTTP 401/403 or E900/E907; classify, preserve a safe code, reject and write a redacted exchange.
- **Failure flow:** Missing local secret is a configuration error and sends no request.
- **Expected result and component:** No blind retry or secret disclosure through the GRA client and transaction state.
- **Acceptance and evidence:** Authentication is distinct from business/temporary errors; client classification tests.

## UC-07 GRA validation or business rejection

- **Actor:** Middleware GRA adapter.
- **Preconditions:** Authenticated request that violates a GRA rule.
- **Trigger and flow:** Preserve the normalized non-retryable code, reject the transaction and record sanitized evidence.
- **Failure flow:** Already-processed E700 enters reconciliation instead of ordinary rejection.
- **Expected result and component:** Stable canonical rejection from response mapping and transaction state.
- **Acceptance and evidence:** E703, E722-E724 and E881 remain visible; business-error mapping tests.

## UC-08 GRA timeout or temporary unavailability

- **Actor:** Middleware transaction service.
- **Preconditions:** Valid outbound request attempted.
- **Trigger and flow:** Timeout, connection failure, HTTP 5xx or rate limit records pending/temporary state and schedules eligible invoice-family recovery.
- **Failure flow:** Notes, statements and inventory remain pending because no authoritative callback is published.
- **Expected result and component:** No blind duplicate through the GRA client, state engine and retry scheduler.
- **Acceptance and evidence:** Temporary and ambiguous failures differ; timeout/retry tests.

## UC-09 Middleware timeout or internal failure

- **Actor:** ERP/API client and support operator.
- **Preconditions:** Request reaches middleware but processing fails.
- **Trigger and flow:** Correlate the request, return a stable problem response and retain safe evidence where possible.
- **Failure flow:** Database loss also fails readiness so traffic can be removed.
- **Expected result and component:** No stack trace, credential or connection string through exception middleware/readiness.
- **Acceptance and evidence:** HTTP 5xx contains `INTERNAL_ERROR` and correlation ID; problem/sanitizer tests.

## UC-10 Controlled retry of transient failure

- **Actor:** Retry worker.
- **Preconditions:** Due eligible job below its attempt limit.
- **Trigger and flow:** Claim atomically, call invoice callback, complete if found, resubmit only a temporary not-found result and apply backoff.
- **Failure flow:** Ambiguous not-found stays reconciliation-only; authentication/business errors are not resubmitted.
- **Expected result and component:** One worker owns the lease and avoids duplicates through retry job/processor.
- **Acceptance and evidence:** Lease expiry and attempt limits work; retry tests, with PostgreSQL concurrency gated.

## UC-11 Duplicate submission

- **Actor:** ERP/API client.
- **Preconditions:** Idempotency key already used for the same tenant and operation.
- **Trigger and flow:** Hash the canonical request; return the original transaction when it matches.
- **Failure flow:** Same key with changed content returns HTTP 409 `IDEMPOTENCY_CONFLICT`.
- **Expected result and component:** One transaction/external submission through the transaction service and uniqueness constraint.
- **Acceptance and evidence:** Tenant/operation scopes prevent unrelated collisions; replay/conflict/concurrency tests.

## UC-12 GRA success followed by lost client response

- **Actor:** ERP client, middleware and retry worker.
- **Preconditions:** GRA may have processed an invoice-family request but its response was lost.
- **Trigger and flow:** Client queries status/reuses the same key; worker checks callback before eligible resubmission.
- **Failure flow:** Callback not-found after an ambiguous failure does not authorize resubmission.
- **Expected result and component:** Recover existing state without a second invoice through lookup/idempotency/reconciliation.
- **Acceptance and evidence:** Original transaction ID is retained; callback-first tests.

## UC-13 Audit and transaction status lookup

- **Actor:** Authenticated tenant client.
- **Preconditions:** Existing tenant data.
- **Trigger and flow:** Query transaction or audit; derive tenant from the credential and filter by tenant ID.
- **Failure flow:** Unknown/other-tenant IDs return not found without disclosing existence.
- **Expected result and component:** Only permitted records through transaction/audit query services.
- **Acceptance and evidence:** Cross-tenant attempts disclose nothing; isolation tests.

## UC-14 Tenant configuration change

- **Actor:** Authorized administrator.
- **Preconditions:** Non-terminated tenant and approved replacement configuration.
- **Trigger and flow:** Validate and persist taxpayer reference/secret name and audit the change.
- **Failure flow:** Terminated tenant rejects changes; plaintext GRA keys are never accepted.
- **Expected result and component:** Only that tenant changes through administration service.
- **Acceptance and evidence:** Subsequent calls use new configuration; administration tests.

## UC-15 GRA API or version change

- **Actor:** Middleware developer/operator.
- **Preconditions:** New authoritative GRA specification.
- **Trigger and flow:** Update the isolated client/mapping, preserve canonical v1 where possible, add tests and document incompatibilities.
- **Failure flow:** Breaking canonical change requires reviewed API version agreement.
- **Expected result and component:** GRA change remains in GraClient rather than Odoo logic.
- **Acceptance and evidence:** Architecture boundaries remain green; ADRs and mapping tests.

## UC-16 Credential compromise or security incident

- **Actor:** Security/operations administrator.
- **Preconditions:** Suspected credential or data exposure.
- **Trigger and flow:** Contain, suspend/revoke/rotate, preserve evidence, assess scope, reconcile pending work, recover and review.
- **Failure flow:** GRA key exposure disables outbound integration until verified rotation.
- **Expected result and component:** Compromised material stops authenticating and is not copied into tickets/logs.
- **Acceptance and evidence:** Exercise records decisions/timestamps; credential tests and incident playbook, with exercise external.

## UC-17 Tenant suspension or termination

- **Actor:** Authorized administrator.
- **Preconditions:** Existing tenant.
- **Trigger and flow:** Suspend for reversible stop or terminate irreversibly; revoke active keys and audit.
- **Failure flow:** Terminated tenant cannot be reactivated through the API.
- **Expected result and component:** Further authentication fails while retained data follows policy.
- **Acceptance and evidence:** All operation families reject the key; lifecycle and remaining-operation tests.

## UC-18 Service recovery after outage

- **Actor:** Operations/support administrator.
- **Preconditions:** Application, database or host outage and approved recovery access.
- **Trigger and flow:** Restore dependencies, validate configuration/migrations/readiness, reclaim leases and reconcile pending transactions before traffic.
- **Failure flow:** Restore backup only under an approved recovery decision.
- **Expected result and component:** Service recovers without unsafe duplicates through readiness/retry/recovery procedures.
- **Acceptance and evidence:** Drill records RTO/RPO observations; lease tests and recovery runbook, with environment drill external.

## UC-19 Scale to many tenants

- **Actor:** Platform/operations team.
- **Preconditions:** Representative isolated data and approved non-production target.
- **Trigger and flow:** Apply safe read/replay load; monitor latency, errors, database and host; tune rate/connection limits.
- **Failure flow:** Diagnose generator versus target saturation; never stress real GRA.
- **Expected result and component:** Tenant isolation and behaviour remain correct through stateless API/PostgreSQL/rate limits.
- **Acceptance and evidence:** Meet approved p95/error thresholds; local safe test procedure, with representative deployment test external.
