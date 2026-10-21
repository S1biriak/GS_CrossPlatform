using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LibraryApi.Data;

public static class LibraryDbFunctions
{
    // This method is translated into SQL by EF. It is never evaluated directly.
    public static bool Contains(string text, string search) => text.Contains(search, StringComparison.OrdinalIgnoreCase);
}

public sealed class SqliteFunctionsInterceptor : DbConnectionInterceptor
{
    private static void Register(DbConnection connection)
    {
        if (connection is SqliteConnection sqlite)
            sqlite.CreateFunction<string, string, bool>("dotnet_contains", LibraryDbFunctions.Contains, isDeterministic: true);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Register(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Register(connection);
        return Task.CompletedTask;
    }
}
