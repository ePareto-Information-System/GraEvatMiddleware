# Architecture

The middleware uses five production projects with inward-facing dependencies:

```text
Api -> Application
Api -> Infrastructure
Api -> GraClient
Infrastructure -> Application -> Domain
GraClient -> Application -> Domain
```

`Domain` has no project dependencies. `Application` defines use cases and outbound interfaces. `Infrastructure` implements persistence, authentication, secrets and auditing. `GraClient` isolates the GRA v8.2 transport contract. `Api` is the HTTP host and composition root.

The Odoo-facing tenant credential and the tenant's GRA security key are separate trust boundaries. Tenant identity is resolved from the middleware credential rather than accepted from request data.
