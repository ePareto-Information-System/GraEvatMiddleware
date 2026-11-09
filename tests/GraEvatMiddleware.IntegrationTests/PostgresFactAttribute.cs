namespace GraEvatMiddleware.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class PostgresFactAttribute : FactAttribute
{
    internal const string ConnectionStringVariable = "GRA_EVAT_TEST_DB_CONNECTION";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Set {ConnectionStringVariable} to an isolated PostgreSQL test database to run this test.";
        }
    }
}
