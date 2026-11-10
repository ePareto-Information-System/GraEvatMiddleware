# ADR 0005 Callback First Reconciliation

**Status:** Accepted.

**Decision:** For invoice-family uncertainty, call the documented GRA callback before considering resubmission.

**Reason:** A timeout does not prove GRA rejected the invoice; immediate resubmission can create a duplicate tax document.

**Consequences:** Ambiguous not-found results remain reconciliation-only. Only a temporary failure may be resubmitted after callback confirms not found.
