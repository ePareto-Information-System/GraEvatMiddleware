# Safe Performance Testing

## Purpose and safety boundary

The `GRA eVAT Safe Performance` collection measures local middleware behaviour without enabling or calling GRA. It contains liveness, readiness, authenticated transaction/audit reads, an idempotent replay and a validation rejection. Run it only in an approved non-production environment with synthetic data.

Do not run it against GRA staging or production. Do not use an idempotency key that has not already created the exact replay transaction.

## Proposed local baseline

Until management approves service-level objectives, use these developer-machine regression thresholds:

- fixed 1 virtual user for 1 minute on a developer workstation;
- p95 below 750 milliseconds;
- p99 below 1,500 milliseconds;
- error rate below 1 percent;
- at least 5 requests per second.

These are not production capacity commitments.

## Setup and execution

1. Start locally with `GraApi:Enabled=false`, `GraRetry:WorkerEnabled=false` and a synthetic tenant.
2. Submit the exact invoice body once using a unique replay key. Retain its transaction ID, key and invoice number.
3. Put `apiKey`, `transactionId`, `replayIdempotencyKey` and `replayInvoiceNumber` only in a temporary untracked copy of the environment file or an approved Postman secret store.
4. Disable application rate limiting only for the throughput baseline so it measures middleware/database work rather than the intentional 120-request control. Run the existing rate-limit integration test separately.

```powershell
postman performance run '.\postman\collections\GRA eVAT Safe Performance' --runner local --environment '.\postman\environments\Safe Performance.environment.yaml' --load-profile fixed --peak-vu 1 --duration 1 --pass-if "less_than(p95, 750)" --pass-if "less_than(p99, 1500)" --pass-if "less_than(error_rate, 1)" --pass-if "greater_than_eq(rps, 5)" --output ndjson
```

## Evidence record

Record date, commit, .NET/PostgreSQL versions, CPU/memory, database location, VUs, duration, request count, RPS, average/p90/p95/p99, error rate, threshold verdict and observed host/database limits. Distinguish load-generator saturation from target saturation.

The local baseline does not establish production capacity. Repeat on the selected topology with representative tenant/data volume and approved aggregate limits. Real GRA stays outside the load path.

## Recorded local evidence

On 10 November 2026 the one-VU fixed run completed 810 requests in 61 seconds with zero transport, script or assertion errors. It achieved 13.28 requests/second, 9 ms average, 12 ms p90, 14 ms p95, 24 ms p99 and 116 ms maximum. All proposed thresholds passed.

An earlier five-VU attempt reached 520 requests with zero request errors and approximately 29 ms average response time before Postman stopped the run because the local load-generator CPU reached 100 percent. That attempt is generator-limited and is not evidence of middleware capacity. A multi-VU test must be repeated on a suitably sized load generator and target-like environment.
