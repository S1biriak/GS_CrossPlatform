using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Services;

public sealed class CatalogService(LibraryDbContext db)
{
    private IQueryable<BookDto> BookDtos(IQueryable<Book> source) => source.Select(b => new BookDto(
        b.Id, b.InventoryNumber, b.Title, b.Author, b.Genre, b.PublicationYear, b.IsArchived,
        !b.IsArchived && !db.Loans.Any(l => l.BookId == b.Id && l.ReturnedAtUtc == null)));

    private static IQueryable<ReaderDto> ReaderDtos(IQueryable<Reader> source) => source.Select(r =>
        new ReaderDto(r.Id, r.CardNumber, r.FullName, r.Email, r.IsBlocked));

    public Task<PagedResult<BookDto>> Books(BookQuery query, CancellationToken ct)
    {
        var books = db.Books.AsNoTracking().AsQueryable();
        if (!query.IncludeArchived) books = books.Where(b => !b.IsArchived);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // SQLite lower() is ASCII-only. The registered function provides
            // case-insensitive Unicode search, including Russian book titles.
            var search = query.Search.Trim();
            books = books.Where(b => LibraryDbFunctions.Contains(b.Title, search) ||
                LibraryDbFunctions.Contains(b.Author, search) || LibraryDbFunctions.Contains(b.InventoryNumber, search));
        }
        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            var genre = query.Genre.Trim();
            books = books.Where(b => b.Genre == genre);
        }
        if (query.AvailableOnly)
            books = books.Where(b => !b.IsArchived && !db.Loans.Any(l => l.BookId == b.Id && l.ReturnedAtUtc == null));
        return BookDtos(books.OrderBy(b => b.Title).ThenBy(b => b.Id)).PageAsync(query, ct);
    }

    public async Task<BookDto> Book(int id, CancellationToken ct) =>
        await BookDtos(db.Books.AsNoTracking().Where(b => b.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw DomainException.NotFound("Книга");

    public async Task<BookDto> CreateBook(CreateBookRequest input, CancellationToken ct)
    {
        var book = new Book(input.InventoryNumber, input.Title, input.Author, input.Genre, input.PublicationYear);
        db.Books.Add(book);
        await db.SaveChangesAsync(ct);
        return await Book(book.Id, ct);
    }

    public async Task<BookDto> UpdateBook(int id, UpdateBookRequest input, CancellationToken ct)
    {
        var book = await FindBook(id, ct);
        book.UpdateDetails(input.Title, input.Author, input.Genre, input.PublicationYear);
        await db.SaveChangesAsync(ct);
        return await Book(id, ct);
    }

    public async Task<BookDto> ArchiveBook(int id, bool archive, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var book = await FindBook(id, ct);
        if (archive) book.Archive(await db.Loans.AnyAsync(l => l.BookId == id && l.ReturnedAtUtc == null, ct));
        else book.Restore();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await Book(id, ct);
    }

    public async Task DeleteBook(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var book = await FindBook(id, ct);
        if (await db.Loans.AnyAsync(l => l.BookId == id, ct))
            throw new DomainException("Книга связана с историей выдач. Используйте архивирование.");
        db.Books.Remove(book);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public Task<PagedResult<ReaderDto>> Readers(ReaderQuery query, CancellationToken ct)
    {
        var readers = db.Readers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            readers = readers.Where(r => LibraryDbFunctions.Contains(r.FullName, search) ||
                LibraryDbFunctions.Contains(r.CardNumber, search));
        }
        return ReaderDtos(readers.OrderBy(r => r.FullName).ThenBy(r => r.Id)).PageAsync(query, ct);
    }

    public async Task<ReaderDto> Reader(int id, CancellationToken ct) =>
        await ReaderDtos(db.Readers.AsNoTracking().Where(r => r.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw DomainException.NotFound("Читатель");

    public async Task<ReaderDto> CreateReader(CreateReaderRequest input, CancellationToken ct)
    {
        var reader = new Reader(input.CardNumber, input.FullName, input.Email);
        db.Readers.Add(reader);
        await db.SaveChangesAsync(ct);
        return await Reader(reader.Id, ct);
    }

    public async Task<ReaderDto> UpdateReader(int id, UpdateReaderRequest input, CancellationToken ct)
    {
        var reader = await FindReader(id, ct);
        reader.UpdateContacts(input.FullName, input.Email);
        await db.SaveChangesAsync(ct);
        return await Reader(id, ct);
    }

    public async Task<ReaderDto> BlockReader(int id, bool block, CancellationToken ct)
    {
        var reader = await FindReader(id, ct);
        if (block) reader.Block();
        else reader.Unblock();
        await db.SaveChangesAsync(ct);
        return await Reader(id, ct);
    }

    public async Task DeleteReader(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var reader = await FindReader(id, ct);
        if (await db.Loans.AnyAsync(l => l.ReaderId == id, ct))
            throw new DomainException("Читатель связан с историей выдач. Используйте блокировку.");
        db.Readers.Remove(reader);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Book> FindBook(int id, CancellationToken ct) =>
        await db.Books.SingleOrDefaultAsync(b => b.Id == id, ct) ?? throw DomainException.NotFound("Книга");

    private async Task<Reader> FindReader(int id, CancellationToken ct) =>
        await db.Readers.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw DomainException.NotFound("Читатель");
}
