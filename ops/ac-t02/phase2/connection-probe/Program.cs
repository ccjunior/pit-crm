using System.Net;
using Npgsql;

if (args.Length == 3 && args[0] == "sanitize-test-log")
{
    return TestLogSanitizer.Run(args[1], args[2], Console.Out, Console.Error);
}

if (args.Length != 3
    || !int.TryParse(args[2], out var expectedPort)
    || !IPAddress.TryParse(args[1], out var expectedAddress))
{
    Console.Error.WriteLine("DB identity probe arguments are invalid.");
    return 2;
}

var testConnectionString = Environment.GetEnvironmentVariable("PITCRM_TEST_CONNECTION");
var applicationConnectionString = Environment.GetEnvironmentVariable("PITCRM_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(testConnectionString)
    || !string.Equals(testConnectionString, applicationConnectionString, StringComparison.Ordinal))
{
    Console.Error.WriteLine("DB identity probe requires matching, process-scoped test and application connections.");
    return 2;
}

try
{
    var builder = new NpgsqlConnectionStringBuilder(testConnectionString);
    if (!string.Equals(builder.Host, "pitcrm-test-db", StringComparison.Ordinal)
        || builder.Port != expectedPort
        || !string.Equals(builder.Database, "pitcrm_test", StringComparison.Ordinal)
        || !string.Equals(builder.Username, "pitcrm_test", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("DB identity probe rejected an unexpected host, port, database, or user.");
        return 2;
    }

    await using var connection = new NpgsqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(
        "SELECT current_database(), current_user, host(inet_server_addr()), inet_server_port()", connection);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync()
        || !string.Equals(reader.GetString(0), "pitcrm_test", StringComparison.Ordinal)
        || !string.Equals(reader.GetString(1), "pitcrm_test", StringComparison.Ordinal)
        || !IPAddress.TryParse(reader.GetString(2), out var actualAddress)
        || !actualAddress.Equals(expectedAddress)
        || reader.GetInt32(3) != expectedPort)
    {
        Console.Error.WriteLine("DB identity probe did not match the expected disposable PostgreSQL container.");
        return 2;
    }

    Console.WriteLine("DB identity probe passed for the expected disposable database and container address.");
    return 0;
}
catch
{
    Console.Error.WriteLine("DB identity probe failed; connection details are suppressed.");
    return 1;
}
