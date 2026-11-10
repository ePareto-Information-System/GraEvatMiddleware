# Configuration Reference

Production values belong in the selected configuration/secret platform, not committed JSON.

| Setting | Secret | Purpose and safe default |
| --- | --- | --- |
| `ConnectionStrings:Database` | Yes | PostgreSQL application connection; TLS/least privilege required in production |
| `Security:AdminKey` | Yes | Administrative API credential; must be strong and distinct from pepper |
| `Security:ApiKeyPepper` | Yes | HMAC pepper for tenant keys; loss/rotation needs a credential plan |
| `GraSecrets:<name>` | Yes | Tenant GRA security key for local development only; managed provider required in production |
| `GraApi:Enabled` | No | `false` until environment/credentials are approved |
| `GraApi:Environment` | No | Only `Staging` is currently accepted |
| `GraApi:BaseUrl` | No | Approved HTTPS GRA base URL |
| `GraApi:AllowedHosts` | No | Exact approved host allowlist |
| `GraApi:RequestTimeoutSeconds` | No | 1-120 seconds; default 30 |
| `GraRetry:Enabled` | No | Allows eligible job scheduling |
| `GraRetry:WorkerEnabled` | No | Enables processing separately from scheduling |
| `GraRetry:MaximumAttempts` | No | Maximum controlled attempts |
| `GraRetry:InitialDelaySeconds` | No | Initial backoff |
| `GraRetry:BackoffMultiplier` | No | Exponential multiplier |
| `GraRetry:MaximumDelaySeconds` | No | Backoff cap |
| `GraRetry:PollIntervalSeconds` | No | Worker poll period |
| `GraRetry:LeaseSeconds` | No | Cross-instance claim lease |
| `RateLimiting:Enabled` | No | Keep enabled except isolated performance baseline |
| `RateLimiting:PermitLimit` | No | Per-window limit per credential/address |
| `RateLimiting:WindowSeconds` | No | Fixed-window duration |
| `RateLimiting:QueueLimit` | No | Default zero to reject overload promptly |
| `ExchangeLogging:Enabled` | No | Enables sanitized exchange persistence |
| `ExchangeLogging:CaptureBodies` | No | Disable to retain metadata only |
| `ExchangeLogging:MaximumBodyCharacters` | No | Stored body size limit |
| `ExchangeLogging:RetentionDays` | No | Operational value pending approved policy |
| cleanup timing settings | No | Retention worker scheduling |

Startup validation rejects missing/placeholder/matching security material and unsafe enabled-GRA settings. Environment-variable form uses double underscores, for example `GraApi__Enabled=false`.
