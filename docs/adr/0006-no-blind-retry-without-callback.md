# ADR 0006 No Blind Retry Without Callback

**Status:** Accepted.

**Decision:** Do not automatically resubmit credit/debit notes, statements or inventory after an ambiguous transport result.

**Reason:** The published GRA contract provides no authoritative lookup for these operations.

**Consequences:** Such transactions remain `PendingGra` for operator/GRA reconciliation. Availability is traded for duplicate prevention.
