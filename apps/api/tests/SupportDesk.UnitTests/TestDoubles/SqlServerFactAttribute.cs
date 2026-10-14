namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// A test that needs a real SQL Server - for SQL only SQL Server speaks. Skipped unless
/// <see cref="ConnectionStringVariable"/> holds a connection string (no database name needed:
/// each test creates and drops its own), so the default test run needs no database.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "SUPPORTDESK_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Needs SQL Server: set {ConnectionStringVariable} to a connection string to run it.";
        }
    }
}
