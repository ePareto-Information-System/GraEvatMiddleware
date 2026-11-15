# Tenant Onboarding Runbook

## Preconditions

- Approved tenant identity and GRA taxpayer reference
- Approved GRA environment and security key
- Unique tenant slug
- Authorized operator with the middleware admin key
- Secure channel for delivering the one-time middleware API key

## Provisioning

1. Store the GRA security key in the approved secret store under a tenant-specific name.
2. Register the tenant with its name, slug, taxpayer reference and secret name.
3. Capture the returned middleware API key once and deliver it through the approved secure channel.
4. Configure the Odoo or Python client with the middleware URL and tenant API key, not the GRA key.
5. Verify authentication, invalid-request handling and transaction lookup before invoice testing.
6. Run a controlled staging invoice and reconcile its status.

## Credential lifecycle

Use the rotation endpoint to revoke one active credential and issue its replacement atomically. The selected old key stops working immediately. Use revocation for a single key, suspension for a reversible tenant-wide stop, and termination only for an irreversible tenant closure.

## Configuration changes

Use the authorized configuration endpoint when the taxpayer reference or GRA secret name changes. Store the new GRA key separately first. Configuration changes are audited and cannot be applied to a terminated tenant.

## Offboarding

Suspend the tenant while coordinating offboarding. Reconcile pending transactions, export only approved operational evidence, apply the approved retention/deletion policy, then terminate the tenant. Termination revokes every active middleware credential.
