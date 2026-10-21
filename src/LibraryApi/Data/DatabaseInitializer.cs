using LibraryApi.Auth;
using LibraryApi.Models;
using LibraryApi.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibraryApi.Data;

public static class DatabaseInitializer
{
    public static async Task Initialize(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.Database.MigrateAsync();
        var demo = scope.ServiceProvider.GetRequiredService<IOptions<DemoOptions>>().Value;
        if (!demo.Seed) return;
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var (login, role, password) in new[]
        {
            ("admin", Roles.Admin, demo.AdminPassword), ("user", Roles.User, demo.UserPassword)
        })
        {
            if (await db.Users.AnyAsync(u => u.Login == login)) continue;
            if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
                throw new InvalidOperationException($"Для первого запуска задайте Demo:{role}Password длиной от 10 символов (scripts/setup-demo.ps1).");
            var user = new AppUser { Login = login, Role = role };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
        if (!await db.Books.AnyAsync() && !await db.Readers.AnyAsync())
            await SeedLibrary(db, scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        await transaction.CommitAsync();
    }

    private static async Task SeedLibrary(LibraryDbContext db, DateTime now)
    {
        var books = new[]
        {
            new Book("LIB-001", "Мастер и Маргарита", "Михаил Булгаков", "Роман", 1967),
            new Book("LIB-002", "Мастер и Маргарита", "Михаил Булгаков", "Роман", 2020),
            new Book("LIB-003", "Пикник на обочине", "Аркадий и Борис Стругацкие", "Фантастика", 1972),
            new Book("LIB-004", "Чистый код", "Роберт Мартин", "Программирование", 2008),
            new Book("LIB-005", "Преступление и наказание", "Фёдор Достоевский", "Роман", 1866),
            new Book("LIB-006", "Евгений Онегин", "Александр Пушкин", "Поэзия", 1833),
            new Book("LIB-007", "Солярис", "Станислав Лем", "Фантастика", 1961),
            new Book("LIB-008", "Алгоритмы", "Томас Кормен", "Программирование", 2009),
            new Book("LIB-009", "Капитанская дочка", "Александр Пушкин", "Повесть", 1836),
            new Book("LIB-010", "Герой нашего времени", "Михаил Лермонтов", "Роман", 1840),
            new Book("LIB-011", "Война и мир", "Лев Толстой", "Роман", 1869)
        };
        books[10].Archive(false);
        var readers = new[]
        {
            new Reader("R-001", "Анна Иванова", "anna@example.test"),
            new Reader("R-002", "Илья Петров", "ilya@example.test"),
            new Reader("R-003", "Мария Соколова", null),
            new Reader("R-004", "Алексей Смирнов", null),
            new Reader("R-005", "Ольга Кузнецова", null)
        };
        readers[2].Block();
        db.Books.AddRange(books);
        db.Readers.AddRange(readers);
        await db.SaveChangesAsync();
        var returned = Loan.Create(books[0].Id, readers[0].Id, now.AddDays(-40), now.AddDays(-26));
        returned.Return(now.AddDays(-28));
        db.Loans.Add(returned);
        db.Loans.Add(Loan.Create(books[0].Id, readers[0].Id, now.AddDays(-5), now.AddDays(9)));
        db.Loans.Add(Loan.Create(books[2].Id, readers[1].Id, now.AddDays(-20), now.AddDays(-6)));
        for (var i = 3; i <= 7; i++)
            db.Loans.Add(Loan.Create(books[i].Id, readers[3].Id, now.AddDays(-2), now.AddDays(12)));
        await db.SaveChangesAsync();
    }
}
