# Release Checklist

## Build and review

- [ ] Release revision and PR approval recorded.
- [ ] No unrelated or uncommitted changes.
- [ ] Formatting, Release build, full automated tests and architecture tests pass.
- [ ] OpenAPI/Postman lint passes and Swagger is visually verified.
- [ ] Dependency vulnerability and secret/attribution scans pass.
- [ ] Test evidence records exact passed, skipped and gated results.

## Configuration and data

- [ ] Target environment, TLS, hosts, database and secret provider approved.
- [ ] Secrets supplied outside source control and values are distinct.
- [ ] Backup completed and checksum/evidence recorded.
- [ ] Migration reviewed and applied once by the deployment identity.
- [ ] Rollback compatibility and recovery decision documented.

## Deployment verification

- [ ] Liveness and readiness pass.
- [ ] Missing/invalid admin and tenant credentials are rejected.
- [ ] Valid tenant authentication and transaction lookup pass.
- [ ] Monitoring/log routing and alert ownership verified.
- [ ] GRA/retry switches remain off until explicitly authorized.
- [ ] Controlled staging smoke test completed when approved.

## Handover

- [ ] Release commit/configuration version recorded.
- [ ] Known issues and external gates recorded.
- [ ] Support/on-call and rollback decision authority confirmed.
- [ ] No critical/high security finding remains.
