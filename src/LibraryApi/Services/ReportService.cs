using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Services;

public sealed class ReportService(LibraryDbContext db, TimeProvider clock)
{
    public Task<PagedResult<ActiveLoanDto>> Active(PageQuery page, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var query = db.Loans.AsNoTracking().Where(l => l.ReturnedAtUtc == null)
            .Join(db.Books, l => l.BookId, b => b.Id, (l, b) => new { Loan = l, Book = b })
            .Join(db.Readers, x => x.Loan.ReaderId, r => r.Id, (x, r) => new { x.Loan, x.Book, Reader = r })
            .OrderBy(x => x.Loan.DueAtUtc).ThenBy(x => x.Loan.Id)
            .Select(x => new ActiveLoanDto(x.Loan.Id, x.Book.Id, x.Book.InventoryNumber, x.Book.Title,
                x.Reader.Id, x.Reader.FullName, x.Loan.IssuedAtUtc, x.Loan.DueAtUtc, x.Loan.DueAtUtc < now));
        return query.PageAsync(page, ct);
    }

    public async Task<PagedResult<OverdueLoanDto>> Overdue(PageQuery page, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var query = db.Loans.AsNoTracking().Where(l => l.ReturnedAtUtc == null && l.DueAtUtc < now)
            .Join(db.Books, l => l.BookId, b => b.Id, (l, b) => new { Loan = l, Book = b })
            .Join(db.Readers, x => x.Loan.ReaderId, r => r.Id, (x, r) => new { x.Loan, x.Book, Reader = r })
            .OrderBy(x => x.Loan.DueAtUtc).ThenBy(x => x.Loan.Id)
            .Select(x => new { x.Loan.Id, x.Book.InventoryNumber, x.Book.Title, ReaderId = x.Reader.Id,
                x.Reader.FullName, x.Loan.DueAtUtc });
        // Only the requested page is materialized. Ceiling is evaluated in .NET,
        // avoiding provider-specific date arithmetic in SQLite.
        var result = await query.PageAsync(page, ct);
        return new(result.Items.Select(x => new OverdueLoanDto(x.Id, x.InventoryNumber, x.Title,
            x.ReaderId, x.FullName, x.DueAtUtc, (int)Math.Ceiling((now - x.DueAtUtc).TotalDays))).ToList(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public Task<PagedResult<PopularBookDto>> Popular(ReportQuery filter, CancellationToken ct)
    {
        var loans = FilterPeriod(filter);
        var query = loans.Join(db.Books, l => l.BookId, b => b.Id, (l, b) => new { Loan = l, Book = b })
            .GroupBy(x => new { x.Book.Id, x.Book.InventoryNumber, x.Book.Title, x.Book.Author })
            .Select(g => new { BookId = g.Key.Id, g.Key.InventoryNumber, g.Key.Title, g.Key.Author, LoanCount = g.Count() })
            .OrderByDescending(x => x.LoanCount).ThenBy(x => x.BookId)
            .Select(x => new PopularBookDto(x.BookId, x.InventoryNumber, x.Title, x.Author, x.LoanCount));
        return query.PageAsync(filter, ct);
    }

    public Task<PagedResult<ReaderStatisticsDto>> Readers(ReportQuery filter, CancellationToken ct)
    {
        var loans = FilterPeriod(filter);
        var now = clock.GetUtcNow().UtcDateTime;
        // Correlated counts preserve readers with no loans, like a left join.
        var query = db.Readers.AsNoTracking().OrderBy(r => r.FullName).ThenBy(r => r.Id)
            .Select(r => new ReaderStatisticsDto(r.Id, r.CardNumber, r.FullName,
                loans.Count(l => l.ReaderId == r.Id),
                loans.Count(l => l.ReaderId == r.Id && l.ReturnedAtUtc != null),
                loans.Count(l => l.ReaderId == r.Id && l.ReturnedAtUtc == null),
                loans.Count(l => l.ReaderId == r.Id && l.ReturnedAtUtc == null && l.DueAtUtc < now)));
        return query.PageAsync(filter, ct);
    }

    private IQueryable<Loan> FilterPeriod(ReportQuery filter)
    {
        if (filter.From.HasValue && filter.To.HasValue && filter.From >= filter.To)
            throw DomainException.Invalid("Начало периода должно предшествовать его концу.");
        var loans = db.Loans.AsNoTracking().AsQueryable();
        if (filter.From is { } from) loans = loans.Where(l => l.IssuedAtUtc >= from.UtcDateTime);
        if (filter.To is { } to) loans = loans.Where(l => l.IssuedAtUtc < to.UtcDateTime);
        return loans;
    }
}
