using System.ComponentModel.DataAnnotations;

namespace LibraryApi.Dtos;

public class PageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class BookQuery : PageQuery
{
    [MaxLength(200)] public string? Search { get; set; }
    [MaxLength(80)] public string? Genre { get; set; }
    public bool AvailableOnly { get; set; }
    public bool IncludeArchived { get; set; }
}

public sealed class ReaderQuery : PageQuery
{
    [MaxLength(150)] public string? Search { get; set; }
}

public sealed class LoanQuery : PageQuery
{
    [Range(1, int.MaxValue)] public int? ReaderId { get; set; }
    [Range(1, int.MaxValue)] public int? BookId { get; set; }
    public bool ActiveOnly { get; set; }
}

public sealed class ReportQuery : PageQuery
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public class UpdateBookRequest
{
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [Required, MaxLength(150)] public string Author { get; set; } = "";
    [Required, MaxLength(80)] public string Genre { get; set; } = "";
    [Range(1, 9999)] public int PublicationYear { get; set; }
}

public sealed class CreateBookRequest : UpdateBookRequest
{
    [Required, MaxLength(40)] public string InventoryNumber { get; set; } = "";
}

public sealed record BookDto(int Id, string InventoryNumber, string Title, string Author,
    string Genre, int PublicationYear, bool IsArchived, bool IsAvailable);

public class UpdateReaderRequest
{
    [Required, MaxLength(150)] public string FullName { get; set; } = "";
    [EmailAddress, MaxLength(254)] public string? Email { get; set; }
}

public sealed class CreateReaderRequest : UpdateReaderRequest
{
    [Required, MaxLength(40)] public string CardNumber { get; set; } = "";
}

public sealed record ReaderDto(int Id, string CardNumber, string FullName, string? Email, bool IsBlocked);

public sealed class CreateLoanRequest
{
    [Range(1, int.MaxValue)] public int BookId { get; set; }
    [Range(1, int.MaxValue)] public int ReaderId { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
}

public sealed class UpdateLoanRequest
{
    [Required] public DateTimeOffset? DueAtUtc { get; set; }
}

public sealed record LoanDto(int Id, int BookId, int ReaderId, DateTime IssuedAtUtc,
    DateTime DueAtUtc, DateTime? ReturnedAtUtc, bool IsOverdue);

public sealed record ActiveLoanDto(int LoanId, int BookId, string InventoryNumber, string BookTitle,
    int ReaderId, string ReaderName, DateTime IssuedAtUtc, DateTime DueAtUtc, bool IsOverdue);

public sealed record OverdueLoanDto(int LoanId, string InventoryNumber, string BookTitle,
    int ReaderId, string ReaderName, DateTime DueAtUtc, int OverdueDays);

public sealed record PopularBookDto(int BookId, string InventoryNumber, string Title, string Author, int LoanCount);

public sealed record ReaderStatisticsDto(int ReaderId, string CardNumber, string FullName,
    int TotalLoans, int ReturnedLoans, int ActiveLoans, int OverdueLoans);

public sealed class LoginRequest
{
    [Required, MaxLength(80)] public string Login { get; set; } = "";
    [Required, MaxLength(200)] public string Password { get; set; } = "";
}

public sealed record UserDto(int Id, string Login, string Role);
public sealed record TokenDto(string AccessToken, DateTime ExpiresAtUtc, UserDto User);
