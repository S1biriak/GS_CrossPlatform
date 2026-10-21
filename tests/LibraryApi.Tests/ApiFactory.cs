using LibraryApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryApi.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Key = "test-only-signing-key-64-characters-minimum-library-api-0123456789-abcdef";
    public const string AdminPassword = "Test-Admin-2026!";
    public const string UserPassword = "Test-User-2026!";
    public static readonly DateTime Now = DateTime.UtcNow;
    private readonly string database = Path.Combine(Path.GetTempPath(), $"library-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Library"] = $"Data Source={database};Pooling=False;Default Timeout=5;Foreign Keys=True",
            ["Jwt:Key"] = Key,
            ["Jwt:Issuer"] = "LibraryApi",
            ["Jwt:Audience"] = "LibraryApi.Client",
            ["Demo:Seed"] = "true",
            ["Demo:Swagger"] = "true",
            ["Demo:AdminPassword"] = AdminPassword,
            ["Demo:UserPassword"] = UserPassword,
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware"] = "None",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore.Update"] = "None"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FixedClock());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(database + suffix);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
