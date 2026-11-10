# Developer Guide

## Start here

The solution separates HTTP/API, application contracts, domain rules, infrastructure and GRA transport. New business behaviour belongs in Domain/Application first; persistence belongs in Infrastructure; GRA-specific paths/payloads stay in GraClient; controllers remain thin.

Build and test from the repository root:

```powershell
dotnet tool restore; dotnet build .\GraEvatMiddleware.slnx; dotnet test .\GraEvatMiddleware.slnx
```

Configure secrets with user secrets for development. Never commit keys or passwords. Apply migrations explicitly with the documented EF command; the API does not migrate the production database on startup.

## Change workflow

1. Start from the current base and create a purpose-named feature branch.
2. Update the canonical contract before intentional API changes.
3. Write the smallest failing test; implement and refactor.
4. Preserve tenant derivation from credentials and state-changing idempotency.
5. Add exact GRA mapping tests for any upstream change.
6. Run formatting, Release build, full tests, Postman/OpenAPI lint, vulnerability and attribution/secret checks.
7. Verify Swagger in a browser, commit focused changes and open a PR for manual merge.

## Adding an operation

Define canonical request/response contracts, validation, authorization and idempotency before transport mapping. Add a domain operation only when persistent state is required. Extend `IGraEvatClient`, map the exact documented GRA path/header/body, sanitize logging, classify responses and document retry/reconciliation. Never infer a callback that GRA does not publish.

## Testing boundaries

Ordinary tests use controlled clients/in-memory persistence. PostgreSQL concurrency tests require a database whose name clearly identifies it as test-only. Real staging tests are category-gated and require explicit enablement. Never include GRA in performance load.

## Documentation ownership

`openapi-v1.yaml` is the machine-readable canonical contract; Postman adds runnable examples/assertions. Keep README, API contract, field mapping, traceability and test evidence aligned with actual runtime behaviour.
