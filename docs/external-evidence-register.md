# External Evidence Register

| Evidence | Owner | Required result | Status |
| --- | --- | --- | --- |
| C# deviation approval | Management/architecture | Governing requirements updated or deviation approved | Open |
| API Contract v1 approval | Middleware and Odoo developers/product owner | Signed/reviewed endpoint/schema/status agreement | Open |
| Ghana Card route confirmation | GRA | Confirm authoritative `nationalId` route | Open |
| Tax calculation/rounding rules | GRA/business owner | Approved workbook/rules baseline | Open |
| Retention and privacy decision | Legal/management/security | Periods, access, deletion, holds and notification obligations | Open |
| Production hosting design | Architecture/operations/security | Approved platform, proxy/TLS, networks, DB, secret manager and monitoring | Open |
| PostgreSQL concurrency run | Middleware/DB operator | Three gated tests pass against isolated test database | Open |
| Representative load test | Platform/middleware | Approved thresholds pass on target-like topology | Open |
| Penetration test | Independent security assessor | No unresolved critical/high; retest evidence | Open |
| Backup/restore/DR exercise | Operations/DB owner | Successful isolated recovery with measured evidence | Open |
| Odoo-GRA end-to-end | Both developers and GRA test environment | Success/failure/reconciliation scenarios pass | Open |
| Pilot acceptance | Client/product/operations | Onboarding, usage, defects and handover accepted | Open |
| GRA production certification | GRA/project owner | Written production authorization/credentials | Open |

Evidence references should point to controlled records, not embed credentials or confidential payloads in this repository.
