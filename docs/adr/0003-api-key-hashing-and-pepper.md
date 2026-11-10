# ADR 0003 API Key Hashing and Pepper

**Status:** Accepted.

**Decision:** Return tenant API keys once, store keyed HMAC-SHA256 material rather than plaintext and keep the pepper outside source control and the database.

**Reason:** Database disclosure alone must not reveal usable tenant credentials.

**Consequences:** Lost keys cannot be recovered and must be rotated. Pepper loss invalidates verification; compromise requires planned credential rotation.
