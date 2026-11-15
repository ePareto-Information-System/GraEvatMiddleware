# Requirements Traceability Matrix

This matrix records the implementation evidence available in the repository and separates completed engineering from evidence that requires management, GRA, Odoo, legal or independent-security participation.

Status meanings:

- **Complete** - implemented and supported by repository evidence.
- **Partially complete** - a usable implementation exists but a stated engineering activity remains.
- **External evidence required** - the repository is ready, but completion requires an external party or approved environment.
- **Blocked by decision** - management, legal, architecture or GRA approval is required.

## Middleware work plan EV 01 to EV 20

| ID | Requirement | Implementation and API | Evidence | Status | Remaining action |
| --- | --- | --- | --- | --- | --- |
| EV-01 | GRA requirements baseline | GRA v8.2 adapter and 12-group mapping | `gra-requirements-baseline.md`, `gra-v8.2-mapping.md`, client contract tests | Partially complete | Obtain authoritative tax workbook, certification procedure and confirmation of Ghana Card route. |
| EV-02 | Compliance and data assessment | Data minimisation, redaction and configurable exchange-log retention | `data-classification-retention.md`, threat model, sanitizer tests | Blocked by decision | Legal/management must approve Ghana-specific retention, access and encryption policy. |
| EV-03 | Use-case catalogue | UC-01 to UC-19 documented and linked to tests | `use-case-catalogue.md` | Complete | Jointly review acceptance criteria with the Odoo developer. |
| EV-04 | Architecture and local design | Five-project C# architecture, PostgreSQL, GRA adapter and trust boundaries | `architecture.md`, `adr/` | Partially complete | Management must formally approve C# deviation and select production hosting. |
| EV-05 | Tenant model and isolation | Tenant lifecycle, tenant-derived identity and scoped queries | Admin and tenant APIs; tenant lifecycle/integration tests | Complete | Independent authenticated isolation test remains part of penetration testing. |
| EV-06 | API-key/authentication model | HMAC-SHA256 credential hashes, pepper, rotation, revocation and suspension | Authentication services and tenant administration tests | Complete | Select managed operator identity/secret store for production. |
| EV-07 | API Contract v1 | Versioned OpenAPI and Postman collection with 24 operations | `openapi-v1.yaml`, `api-contract-v1.md`, Postman lint | Partially complete | Obtain joint Odoo developer approval of the frozen v1 boundary. |
| EV-08 | Core middleware API | ASP.NET Core API, validation, persistence, errors, logging and monitoring | Source projects and automated test suite | Complete | None for repository implementation. |
| EV-09 | GRA adapter and mapping | Isolated `GraEvatClient` and canonical mapping | `gra-field-mapping.md`, mapping unit tests | Complete | Confirm unresolved upstream documentation discrepancies with GRA. |
| EV-10 | Secure GRA communication | HTTPS, staging host enforcement, allowlist, redirect blocking, secret indirection and timeouts | Client configuration validation and staging-gated tests | External evidence required | Execute approved staging/certification tests and approve production host/certificates. |
| EV-11 | Transaction state and persistence | PostgreSQL transaction state, lookup, references and audit events | EF migrations, transaction tests and API contract | Complete | Approve production retention and encryption requirements. |
| EV-12 | Retry and duplicate prevention | Tenant/operation idempotency, callback-first reconciliation and leased retry jobs | Retry unit/integration tests and gated PostgreSQL concurrency tests | Partially complete | Run three concurrency tests against an isolated PostgreSQL test database. |
| EV-13 | Security controls | Validation, authentication, rate limiting, redaction, secure errors and monitoring | Security checklist, automated security regression tests | Complete | Production perimeter, database encryption and managed identity remain deployment decisions. |
| EV-14 | Threat model and assurance | Threat model and penetration-test scope/acceptance criteria | `threat-model.md`, `security-assurance.md` | External evidence required | Independent penetration test and remediation of all critical/high findings. |
| EV-15 | Automated and assurance testing | Unit, integration, architecture, contract and safe performance assets | `test-evidence.md`, `performance-testing.md` | Partially complete | Run PostgreSQL concurrency and approved real staging/E2E suites. |
| EV-16 | Deployment environments | Environment configuration, release and rollback procedures | `configuration-reference.md`, operations runbook, release checklist | Blocked by decision | Select hosting, proxy, TLS termination, secret manager and service-level targets. |
| EV-17 | Operations package | Monitoring, backup/restore, recovery, incident, patch and rollback procedures | Operations, recovery and incident documents | Partially complete | Perform and record restore/DR/incident exercises in the selected environment. |
| EV-18 | Tenant onboarding package | Repeatable provisioning, credential lifecycle and offboarding | `tenant-onboarding.md`, administrator guide | Complete | Validate process during pilot onboarding. |
| EV-19 | Pilot and production rollout | Readiness checklist and external evidence register | `production-readiness-checklist.md`, `external-evidence-register.md` | External evidence required | Odoo E2E, pilot acceptance, GRA certification and operational handover. |
| EV-20 | Productisation documentation | Developer, consumer, administrator and support guidance | Documentation index in README | Complete | Apply approved branding/publication process if distributed externally. |

## Functional requirements FR 001 to FR 018

| ID | Requirement | Implementation / endpoint | Test or evidence | Status |
| --- | --- | --- | --- | --- |
| FR-001 | Tenant registration | `POST /api/v1/admin/tenants` | Tenant administration tests | Complete |
| FR-002 | Tenant identification | Tenant resolved exclusively from `X-API-Key` | Authentication and isolation tests | Complete |
| FR-003 | Authentication and authorization | Admin and tenant authentication schemes | Missing/invalid key tests | Complete |
| FR-004 | Credential lifecycle | Issue, rotate, revoke, suspend and terminate operations | Tenant lifecycle tests | Complete |
| FR-005 | Request validation | Data annotations and operation-specific validation | Validation tests | Complete |
| FR-006 | Canonical-to-GRA transformation | `GraEvatClient` payload mapping | Client contract tests | Complete |
| FR-007 | Secure GRA submission | HTTPS/host/environment checks and secret header | Configuration and client tests | External evidence required |
| FR-008 | GRA response validation | Success, error and malformed-response mapping | Client response tests | Complete |
| FR-009 | Standardized response | Transaction and identification result contracts | OpenAPI and integration tests | Complete |
| FR-010 | Meaningful errors | RFC 7807 codes and GRA error preservation | Error-path tests | Complete |
| FR-011 | Transaction status | Persistent lifecycle and terminal/intermediate states | Domain and integration tests | Complete |
| FR-012 | Controlled retry | Durable leased jobs and callback-first logic | Retry tests | Complete |
| FR-013 | Duplicate prevention | Tenant/operation idempotency and request hashing | Replay/conflict/concurrency tests | Complete |
| FR-014 | Audit trail | Tenant-aware audit and sanitized exchange records | Logging/audit tests | Complete |
| FR-015 | Status lookup | `GET /api/v1/transactions/{transactionId}` | Tenant-scoped lookup tests | Complete |
| FR-016 | Tenant configuration | Authorized configuration update | Administration tests | Complete |
| FR-017 | Odoo integration | Stable canonical contract exposed | OpenAPI/Postman artifacts | External evidence required |
| FR-018 | Client onboarding | Provisioning and credential instructions | Onboarding runbook | Complete |

## Non-functional requirements NFR 001 to NFR 015

| ID | Requirement | Repository evidence | Status / external gate |
| --- | --- | --- | --- |
| NFR-001 | Tenant isolation | Tenant-derived claims and scoped persistence queries | Complete; penetration confirmation external. |
| NFR-002 | TLS | GRA client enforces HTTPS | Middleware ingress TLS depends on selected proxy/host. |
| NFR-003 | Secret protection | HMAC hashes, pepper and secret indirection | Production secret manager decision outstanding. |
| NFR-004 | Least privilege | Application DB role guidance and separated admin boundary | Production identities/roles require hosting decision. |
| NFR-005 | Validation and abuse protection | Validation plus credential/IP rate limiting | Complete; production limits require tuning. |
| NFR-006 | No sensitive leakage | Recursive redaction, URL masking and stable errors | Complete; independent testing external. |
| NFR-007 | Logging, monitoring and auditing | Structured telemetry, audit and operations status | Complete; central platform selection outstanding. |
| NFR-008 | Horizontal scaling | Stateless API and database retry leases | Design complete; deployed load evidence external. |
| NFR-009 | Clear project structure | API/Application/Domain/Infrastructure/GraClient boundaries | Complete with architecture test. |
| NFR-010 | API versioning | `/api/v1` and OpenAPI v1 | Complete. |
| NFR-011 | Automated tests | Unit, integration, architecture, contract and gated suites | Complete locally; gated suites listed separately. |
| NFR-012 | Configuration over client code | Tenant configuration and options binding | Complete. |
| NFR-013 | Source control and review | Feature branches and pull requests | Complete for repository workflow. |
| NFR-014 | Deployment, rollback and recovery | Runbooks and evidence checklists | Documentation complete; exercises external. |
| NFR-015 | Extensibility | Isolated GRA adapter and canonical API | Complete. |

## Use-case traceability UC 01 to UC 19

| ID | Use case | Primary evidence | Status |
| --- | --- | --- | --- |
| UC-01 | New tenant onboarding | Tenant API tests and onboarding runbook | Complete; pilot validation external. |
| UC-02 | Credential lifecycle | Tenant administration tests | Complete. |
| UC-03 | Successful transaction | Controlled client and gated staging tests | Local complete; real E2E external. |
| UC-04 | Invalid request | Validation tests | Complete. |
| UC-05 | Invalid tenant credential | Authentication tests | Complete. |
| UC-06 | GRA authentication failure | Client error mapping tests | Complete. |
| UC-07 | GRA business rejection | Documented error-code tests | Complete. |
| UC-08 | GRA timeout/unavailability | Pending-state and retry tests | Complete. |
| UC-09 | Middleware internal failure | Stable problem response tests | Complete. |
| UC-10 | Retry transient failure | Retry processor tests | Complete; PostgreSQL concurrency gated. |
| UC-11 | Duplicate submission | Replay/conflict tests | Complete. |
| UC-12 | Lost response after GRA success | Callback-first reconciliation tests | Complete. |
| UC-13 | Audit/status lookup | Tenant-scoped query tests | Complete. |
| UC-14 | Configuration change | Tenant administration tests | Complete. |
| UC-15 | GRA version change | Adapter isolation and ADR | Complete as design control. |
| UC-16 | Credential compromise | Rotation/revocation plus incident playbook | Procedure complete; exercise external. |
| UC-17 | Suspension/termination | Lifecycle and authorization tests | Complete. |
| UC-18 | Service recovery | Lease recovery tests and recovery runbook | Partially complete; restore exercise external. |
| UC-19 | Scale to many tenants | Stateless design, rate limits and performance plan | Partially complete; representative deployment load test external. |
