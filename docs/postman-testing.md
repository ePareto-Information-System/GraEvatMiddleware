# Postman API Testing

Three local collections are maintained:

- `GRA eVAT API Contract` is aligned with `docs/openapi-v1.yaml` and contains all 24 middleware operations, including notes, statements, inventory and identification validation.
- `GRA eVAT Local Workflow` is an ordered nine-request regression workflow covering liveness, readiness, tenant registration, invoice submission, idempotent replay, changed-payload rejection, transaction lookup, tenant-scoped audit lookup and missing-key authentication.
- `GRA eVAT Safe Performance` is a six-request, non-fiscal workload for health checks, authenticated reads, idempotent replay and rejected invalid input. It must be run with GRA disabled and uses empty committed secret placeholders.

The local workflow expects the API at `http://localhost:5189`, PostgreSQL to be available, and `GraApi:Enabled` to remain `false`. It creates unique tenant and invoice values on every run. It does not call GRA.

Enter the local administrative key into the `adminKey` value in Postman without committing it, or pass it only for the run:

```powershell
postman collection run '.\postman\collections\GRA eVAT Local Workflow' -e '.\postman\environments\Local.environment.yaml' --env-var "adminKey=$env:GRA_EVAT_ADMIN_KEY" --no-report-events
```

Keep the actual key in the `GRA_EVAT_ADMIN_KEY` process or user environment variable. The committed environment contains an empty secret placeholder only. The tenant API key is captured from the registration response into a collection runtime variable and is not written to disk.

Validate the artifacts without starting the API:

```powershell
postman spec lint .\docs\openapi-v1.yaml --issueType syntax --no-report-events; postman collection lint '.\postman\collections\GRA eVAT API Contract' --fail-severity warning; postman collection lint '.\postman\collections\GRA eVAT Local Workflow' --fail-severity warning; postman environment lint '.\postman\environments\Local.environment.yaml' --fail-severity warning
```

Postman cloud publication is intentionally not configured. Local artifacts are reviewable in Git and no API key, administrative key or GRA security key is committed.

See `performance-testing.md` for the safe performance command, thresholds, measured local result and test limitations.
