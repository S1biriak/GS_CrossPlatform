using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LibraryApi.Data;

/// <summary>Создание миграций не требует JWT-секрета и запуска HTTP-сервера.</summary>
public sealed class LibraryDesignTimeFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<LibraryDbContext>()
        .UseSqlite("Data Source=App_Data/library.db")
        .AddInterceptors(new SqliteFunctionsInterceptor()).Options);
}
