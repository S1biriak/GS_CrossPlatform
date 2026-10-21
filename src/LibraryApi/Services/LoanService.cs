using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Models;
using LibraryApi.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibraryApi.Services;

public sealed class LoanService(LibraryDbContext db, TimeProvider clock, IOptions<LibraryOptions> options)
{
    private IQueryable<LoanDto> Dtos(IQueryable<Loan> loans)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return loans.Select(l => new LoanDto(l.Id, l.BookId, l.ReaderId, l.IssuedAtUtc, l.DueAtUtc,
            l.ReturnedAtUtc, l.ReturnedAtUtc == null && l.DueAtUtc < now));
    }

    public Task<PagedResult<LoanDto>> List(LoanQuery query, CancellationToken ct)
    {
        var loans = db.Loans.AsNoTracking().AsQueryable();
        if (query.ReaderId.HasValue) loans = loans.Where(l => l.ReaderId == query.ReaderId);
        if (query.BookId.HasValue) loans = loans.Where(l => l.BookId == query.BookId);
        if (query.ActiveOnly) loans = loans.Where(l => l.ReturnedAtUtc == null);
        return Dtos(loans.OrderByDescending(l => l.IssuedAtUtc).ThenBy(l => l.Id)).PageAsync(query, ct);
    }

    public async Task<LoanDto> Get(int id, CancellationToken ct) =>
        await Dtos(db.Loans.AsNoTracking().Where(l => l.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw DomainException.NotFound("Выдача");

    public async Task<LoanDto> Create(CreateLoanRequest input, CancellationToken ct)
    {
        // SQLite begins an immediate transaction: concurrent writers cannot both
        // pass the reader-limit check. A partial unique index also protects BookId.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var book = await db.Books.SingleOrDefaultAsync(b => b.Id == input.BookId, ct)
            ?? throw DomainException.NotFound("Книга");
        var reader = await db.Readers.SingleOrDefaultAsync(r => r.Id == input.ReaderId, ct)
            ?? throw DomainException.NotFound("Читатель");
        book.EnsureCanBeLoaned(await db.Loans.AnyAsync(l => l.BookId == book.Id && l.ReturnedAtUtc == null, ct));
        var activeCount = await db.Loans.CountAsync(l => l.ReaderId == reader.Id && l.ReturnedAtUtc == null, ct);
        reader.EnsureCanBorrow(activeCount, options.Value.ActiveLoanLimit);
        var now = clock.GetUtcNow().UtcDateTime;
        var loan = Loan.Create(book.Id, reader.Id, now, input.DueAtUtc?.UtcDateTime ?? now.AddDays(options.Value.DefaultLoanDays));
        db.Loans.Add(loan);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await Get(loan.Id, ct);
    }

    public async Task<LoanDto> Update(int id, UpdateLoanRequest input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var loan = await Find(id, ct);
        loan.ChangeDueDate(input.DueAtUtc!.Value.UtcDateTime, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await Get(id, ct);
    }

    public async Task<LoanDto> Return(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var loan = await Find(id, ct);
        loan.Return(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await Get(id, ct);
    }

    public async Task Delete(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var loan = await Find(id, ct);
        if (loan.ReturnedAtUtc is null) throw new DomainException("Сначала необходимо вернуть книгу.");
        db.Loans.Remove(loan);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Loan> Find(int id, CancellationToken ct) =>
        await db.Loans.SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw DomainException.NotFound("Выдача");
}
