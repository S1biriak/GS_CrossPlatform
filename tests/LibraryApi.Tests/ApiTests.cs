using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LibraryApi.Tests;

public sealed class ApiTests
{
    [Fact]
    public async Task AccessMatrixDistinguishes401And403()
    {
        using var factory = new ApiFactory();
        using var guest = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/v1/books")).StatusCode);
        foreach (var url in new[] { "/api/v1/readers", "/api/v1/loans", "/api/v1/reports/active-loans",
            "/api/v1/reports/overdue-loans", "/api/v1/reports/popular-books", "/api/v1/reports/reader-statistics" })
        {
            var response = await guest.GetAsync(url);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        using var user = await Authorized(factory, "user");
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/reports/active-loans")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/v1/books", BookInput())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync("/api/v1/books/1", new UpdateBookRequest
            { Title = "Название", Author = "Автор", Genre = "Жанр", PublicationYear = 2020 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.DeleteAsync("/api/v1/books/1")).StatusCode);
        var me = await user.GetFromJsonAsync<UserDto>("/api/v1/auth/me");
        Assert.Equal("User", me!.Role);
        Assert.DoesNotContain("password", await (await user.GetAsync("/api/v1/auth/me")).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidCredentialsTamperedAndExpiredTokensAreRejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Login = "admin", Password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Login = "missing", Password = "wrong" })).StatusCode);
        foreach (var token in new[] { "bad.token.value", ExpiredToken() })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/reports/active-loans")).StatusCode);
        }
    }

    [Fact]
    public async Task CompleteCrudLifecyclePreservesRulesAndAvailability()
    {
        using var factory = new ApiFactory();
        using var admin = await Authorized(factory);
        var created = await admin.PostAsJsonAsync("/api/v1/books", BookInput());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var book = (await created.Content.ReadFromJsonAsync<BookDto>())!;
        Assert.EndsWith($"/api/v1/books/{book.Id}", created.Headers.Location!.ToString());
        Assert.True(book.IsAvailable);
        var reader = await CreateReader(admin);
        var updatedBook = await admin.PutAsJsonAsync($"/api/v1/books/{book.Id}", new UpdateBookRequest
            { Title = "Обновлённая книга", Author = "Автор", Genre = "Тест", PublicationYear = 2024 });
        Assert.Equal(HttpStatusCode.OK, updatedBook.StatusCode);
        Assert.Equal("Обновлённая книга", (await updatedBook.Content.ReadFromJsonAsync<BookDto>())!.Title);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/readers/{reader.Id}",
            new UpdateReaderRequest { FullName = "Новое имя", Email = "changed@example.test" })).StatusCode);
        var loanResponse = await admin.PostAsJsonAsync("/api/v1/loans", new CreateLoanRequest { BookId = book.Id, ReaderId = reader.Id });
        Assert.Equal(HttpStatusCode.Created, loanResponse.StatusCode);
        var loan = (await loanResponse.Content.ReadFromJsonAsync<LoanDto>())!;
        Assert.Equal(DateTimeKind.Utc, loan.IssuedAtUtc.Kind);
        Assert.Equal(ApiFactory.Now.AddDays(14), loan.DueAtUtc);
        Assert.False((await admin.GetFromJsonAsync<BookDto>($"/api/v1/books/{book.Id}"))!.IsAvailable);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/loans",
            new CreateLoanRequest { BookId = book.Id, ReaderId = reader.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/books/{book.Id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/loans/{loan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/readers/{reader.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/loans/{loan.Id}",
            new UpdateLoanRequest { DueAtUtc = ApiFactory.Now.AddDays(20) })).StatusCode);
        // Blocking forbids a new loan but does not interfere with a return.
        await admin.PostAsync($"/api/v1/readers/{reader.Id}/block", null);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/loans/{loan.Id}/return", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/loans/{loan.Id}/return", null)).StatusCode);
        Assert.True((await admin.GetFromJsonAsync<BookDto>($"/api/v1/books/{book.Id}"))!.IsAvailable);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/books/{book.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/loans/{loan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/books/{book.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/readers/{reader.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/loans/{loan.Id}")).StatusCode);
    }

    [Fact]
    public async Task ValidationUniquenessUnicodeSearchAndPaginationWork()
    {
        using var factory = new ApiFactory();
        using var admin = await Authorized(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/books", new { title = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/readers",
            new { cardNumber = "X", fullName = "Имя", email = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/books", new
            { inventoryNumber = "BAD", title = "Книга", author = "Автор", genre = "Тест", publicationYear = 2020, isArchived = true })).StatusCode);
        foreach (var query in new[] { "page=0", "pageSize=101", "page=bad" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/v1/books?{query}")).StatusCode);
        var search = await admin.GetFromJsonAsync<PagedResult<BookDto>>("/api/v1/books?search=" + Uri.EscapeDataString("МАСТЕР"));
        Assert.Equal(2, search!.TotalCount);
        var page = await admin.GetFromJsonAsync<PagedResult<BookDto>>("/api/v1/books?pageSize=1");
        Assert.Single(page!.Items);
        Assert.True(page.TotalCount > 1);
        var hugePage = await admin.GetFromJsonAsync<PagedResult<BookDto>>("/api/v1/books?page=2147483647&pageSize=100");
        Assert.Empty(hugePage!.Items);
        var created = (await (await admin.PostAsJsonAsync("/api/v1/books", BookInput())).Content.ReadFromJsonAsync<BookDto>())!;
        var duplicate = BookInput();
        duplicate.InventoryNumber = created.InventoryNumber.ToLowerInvariant();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/books", duplicate)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/books/2147483647")).StatusCode);
    }

    [Fact]
    public async Task ReportsReturnExactSeededCountsAndHonorPeriodBoundaries()
    {
        using var factory = new ApiFactory();
        using var user = await Authorized(factory, "user");
        var active = await user.GetFromJsonAsync<PagedResult<ActiveLoanDto>>("/api/v1/reports/active-loans");
        Assert.Equal(7, active!.TotalCount);
        Assert.All(active.Items, x => Assert.False(string.IsNullOrEmpty(x.ReaderName)));
        var overdue = await user.GetFromJsonAsync<PagedResult<OverdueLoanDto>>("/api/v1/reports/overdue-loans");
        Assert.Single(overdue!.Items);
        Assert.Equal(6, overdue.Items[0].OverdueDays);
        var popular = await user.GetFromJsonAsync<PagedResult<PopularBookDto>>("/api/v1/reports/popular-books");
        Assert.Equal(7, popular!.TotalCount);
        Assert.Equal("LIB-001", popular.Items[0].InventoryNumber);
        Assert.Equal(2, popular.Items[0].LoanCount);
        var readers = await user.GetFromJsonAsync<PagedResult<ReaderStatisticsDto>>("/api/v1/reports/reader-statistics");
        Assert.Equal(5, readers!.TotalCount);
        Assert.Equal(8, readers.Items.Sum(x => x.TotalLoans));
        Assert.Equal(0, readers.Items.Single(x => x.CardNumber == "R-005").TotalLoans);
        var from = Uri.EscapeDataString(ApiFactory.Now.AddDays(-5).ToString("O"));
        var to = Uri.EscapeDataString(ApiFactory.Now.AddDays(-2).ToString("O"));
        var filtered = await user.GetFromJsonAsync<PagedResult<PopularBookDto>>($"/api/v1/reports/popular-books?from={from}&to={to}");
        Assert.Single(filtered!.Items);
        Assert.Equal(1, filtered.Items[0].LoanCount);
        var empty = await user.GetFromJsonAsync<PagedResult<ReaderStatisticsDto>>(
            "/api/v1/reports/reader-statistics?from=2090-01-01T00:00:00Z&to=2091-01-01T00:00:00Z");
        Assert.Equal(5, empty!.TotalCount);
        Assert.All(empty.Items, x => Assert.Equal(0, x.TotalLoans));
        Assert.Equal(HttpStatusCode.BadRequest, (await user.GetAsync($"/api/v1/reports/popular-books?from={to}&to={from}")).StatusCode);
    }

    [Fact]
    public async Task BlockArchiveAndLimitRejectLoansWithoutChangingState()
    {
        using var factory = new ApiFactory();
        using var admin = await Authorized(factory);
        var book = (await (await admin.PostAsJsonAsync("/api/v1/books", BookInput())).Content.ReadFromJsonAsync<BookDto>())!;
        foreach (var readerId in new[] { 3, 4 })
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/loans",
                new CreateLoanRequest { BookId = book.Id, ReaderId = readerId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/books/{book.Id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/loans",
            new CreateLoanRequest { BookId = book.Id, ReaderId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/books/{book.Id}/restore", null)).StatusCode);
        Assert.True((await admin.GetFromJsonAsync<BookDto>($"/api/v1/books/{book.Id}"))!.IsAvailable);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/loans",
            new CreateLoanRequest { BookId = book.Id, ReaderId = 1, DueAtUtc = ApiFactory.Now.AddDays(-1) })).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRequestsCannotIssueTheSameCopyTwiceOrExceedReaderLimit()
    {
        using var factory = new ApiFactory();
        using var admin = await Authorized(factory);
        var book = (await (await admin.PostAsJsonAsync("/api/v1/books", BookInput())).Content.ReadFromJsonAsync<BookDto>())!;
        var reader = await CreateReader(admin);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => admin.PostAsJsonAsync("/api/v1/loans",
            new CreateLoanRequest { BookId = book.Id, ReaderId = reader.Id })));
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Conflict);
        // Reader 4 starts at 5. Return one loan, leaving exactly one free slot.
        var existing = await admin.GetFromJsonAsync<PagedResult<LoanDto>>("/api/v1/loans?readerId=4&activeOnly=true");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/loans/{existing!.Items[0].Id}/return", null)).StatusCode);
        var copies = new List<BookDto>();
        for (var i = 0; i < 2; i++)
            copies.Add((await (await admin.PostAsJsonAsync("/api/v1/books", BookInput())).Content.ReadFromJsonAsync<BookDto>())!);
        var limitAttempts = await Task.WhenAll(copies.Select(b => admin.PostAsJsonAsync("/api/v1/loans",
            new CreateLoanRequest { BookId = b.Id, ReaderId = 4 })));
        Assert.Single(limitAttempts, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(limitAttempts, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DatabaseIndexRejectsDuplicateActiveLoanEvenWhenServiceIsBypassed()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        db.Loans.Add(Loan.Create(1, 2, ApiFactory.Now, ApiFactory.Now.AddDays(1)));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SwaggerDescribesApiAndForeignKeysAreEnforced()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var swagger = await client.GetStringAsync("/swagger/v1/swagger.json");
        Assert.Contains("/api/v1/reports/overdue-loans", swagger);
        Assert.Contains("bearer", swagger);
        using var spec = JsonDocument.Parse(swagger);
        var routes = spec.RootElement.GetProperty("paths");
        Assert.True(routes.GetProperty("/api/v1/books").GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(routes.GetProperty("/api/v1/books").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(routes.GetProperty("/api/v1/books").GetProperty("get").TryGetProperty("security", out var publicSecurity) && publicSecurity.GetArrayLength() > 0);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        db.Loans.Add(Loan.Create(99999, 1, ApiFactory.Now, ApiFactory.Now.AddDays(1)));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static async Task<HttpClient> Authorized(ApiFactory factory, string login = "admin")
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
            { Login = login, Password = login == "admin" ? ApiFactory.AdminPassword : ApiFactory.UserPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<TokenDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private static CreateBookRequest BookInput() => new()
    {
        InventoryNumber = "TEST-" + Guid.NewGuid().ToString("N"), Title = "Тестовая книга", Author = "Автор", Genre = "Тест", PublicationYear = 2026
    };

    private static async Task<ReaderDto> CreateReader(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/v1/readers", new CreateReaderRequest
            { CardNumber = "TEST-" + Guid.NewGuid().ToString("N"), FullName = "Тестовый читатель", Email = "reader@example.test" }))
            .Content.ReadFromJsonAsync<ReaderDto>())!;

    private static string ExpiredToken()
    {
        var token = new JwtSecurityToken("LibraryApi", "LibraryApi.Client", [new Claim("role", "Admin")],
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
