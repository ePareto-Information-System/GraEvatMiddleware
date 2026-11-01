# GRA eVAT Middleware

Multi-tenant ASP.NET Core middleware that exposes a canonical API for Odoo and integrates with the Ghana Revenue Authority eVAT API.

## Solution projects

- `GraEvatMiddleware.Api` hosts the HTTP API and composition root.
- `GraEvatMiddleware.Application` contains use cases and integration abstractions.
- `GraEvatMiddleware.Domain` contains tenant and transaction business rules.
- `GraEvatMiddleware.Infrastructure` contains persistence, authentication, secrets and operational adapters.
- `GraEvatMiddleware.GraClient` isolates the GRA v8.2 HTTP contract and mapping.

## Current status

The solution structure and dependency boundaries are established. Domain behavior, the canonical API contract, persistence and the GRA adapter will be implemented incrementally with automated tests.

## Build

```powershell
dotnet build GraEvatMiddleware.slnx
dotnet test GraEvatMiddleware.slnx
```
