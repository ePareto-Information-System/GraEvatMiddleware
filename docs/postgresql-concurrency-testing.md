# PostgreSQL Concurrency Testing

These tests exercise PostgreSQL locking and unique constraints that the ordinary in-memory tests cannot prove. They use a controlled GRA client and never transmit an invoice outside the test process.

## Safety boundary

The test harness refuses to start unless `GRA_EVAT_TEST_DB_CONNECTION` names a database ending in `_test` or `_tests`. On startup it migrates and truncates that database, so never point it at `gra_evat` or any shared database.

Create the isolated database as a standalone statement while connected as the PostgreSQL administrator:

```sql
CREATE DATABASE gra_evat_tests WITH OWNER gra_evat_app ENCODING 'UTF8';
```

Run the gated tests from PowerShell after setting the connection string for the current terminal:

```powershell
$env:GRA_EVAT_TEST_DB_CONNECTION='Host=localhost;Port=5434;Database=gra_evat_tests;Username=gra_evat_app;Password=YOUR_APP_PASSWORD'; dotnet test .\tests\GraEvatMiddleware.IntegrationTests\GraEvatMiddleware.IntegrationTests.csproj --filter FullyQualifiedName~PostgresConcurrencyTests
```

Remove the temporary value when finished:

```powershell
Remove-Item Env:GRA_EVAT_TEST_DB_CONNECTION
```

The scenarios prove that simultaneous identical submissions create one transaction and one GRA submission, two retry workers cannot process the same job, and an expired lease can be recovered after a worker restart.
