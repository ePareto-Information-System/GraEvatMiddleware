# Recovery and Business Continuity

## Recovery objectives

Management must approve RTO and RPO. Until then, record measured recovery/backup intervals without presenting them as commitments. Fiscal correctness takes priority over rapid blind resubmission.

## Encrypted backup procedure

1. Confirm backup destination encryption, access and free capacity.
2. Record commit, migration level, database name, PostgreSQL version and UTC start time.
3. Run `pg_dump` with a credential supplied outside command history.
4. Verify non-zero output and backup checksum; restrict file permissions.
5. Retain according to approved policy and monitor backup failures.

Template only; replace approved paths and never commit passwords:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" -h localhost -p 5434 -U gra_evat_app -d gra_evat -F c -f "<approved-encrypted-path>\gra_evat_<timestamp>.backup" -W
```

## Isolated restore drill

Never restore over `gra_evat`. Create a clearly named isolated database using an authorized administrative identity, restore the selected backup, and validate:

- `__EFMigrationsHistory`;
- tenant/credential counts without exposing hashes;
- transaction status/reference integrity;
- audit and exchange record counts;
- retry jobs and lease states;
- application readiness against the restored database with GRA disabled.

```powershell
& "C:\Program Files\PostgreSQL\18\bin\pg_restore.exe" -h localhost -p 5434 -U postgres -d gra_evat_restore_test --clean --if-exists "<approved-backup-file>" -W
```

The command is documentation only. An operator must verify the target name before execution.

## Drill evidence

Record backup ID/checksum, source timestamp, restore target, start/end, measured RTO, observed data point/RPO, migration result, validation queries, issues, remediation owner and approval. Delete the isolated restore database only through an approved recoverable procedure.

## Application outage

Remove unhealthy instances, preserve logs, deploy last known compatible release, verify migrations/configuration/readiness, then reconcile pending transactions before restoring full traffic.

## Database outage

Stop fiscal traffic, preserve connection/host evidence, restore database availability or execute the approved failover, verify migration/data state, reclaim expired retry leases and reconcile pending transactions.

## GRA outage

Keep local intake policy aligned with management approval. Temporary invoice-family failures may schedule controlled recovery; ambiguous results reconcile by callback. Notes/statements/inventory remain pending. Never manually replay with a new idempotency key.

## Business continuity priorities

1. Protect credentials and evidence.
2. Prevent duplicate fiscal documents.
3. Restore authoritative transaction status and database integrity.
4. Resume read/status functions before enabling new fiscal traffic where practical.
5. Communicate the known impact and external dependencies.
