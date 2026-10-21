using LibraryApi.Models;

namespace LibraryApi.Tests;

public sealed class DomainTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LoanValidatesDatesAndIdentifiers()
    {
        Assert.Equal(400, Assert.Throws<DomainException>(() => Loan.Create(1, 1, Now, Now)).StatusCode);
        Assert.Throws<DomainException>(() => Loan.Create(0, 1, Now, Now.AddDays(1)));
        Assert.Throws<DomainException>(() => Loan.Create(1, 1, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), Now.AddDays(1)));
    }

    [Fact]
    public void ReturnIsFinalAndCannotPrecedeIssue()
    {
        var loan = Loan.Create(1, 1, Now, Now.AddDays(1));
        Assert.Throws<DomainException>(() => loan.Return(Now.AddMinutes(-1)));
        loan.Return(Now.AddHours(2));
        Assert.Equal(409, Assert.Throws<DomainException>(() => loan.Return(Now.AddHours(3))).StatusCode);
        Assert.Throws<DomainException>(() => loan.ChangeDueDate(Now.AddDays(2), Now.AddHours(3)));
    }

    [Fact]
    public void OverdueBoundaryAndReturnedHistoryAreConsistent()
    {
        var loan = Loan.Create(1, 1, Now.AddDays(-2), Now);
        Assert.False(loan.IsOverdue(Now));
        Assert.True(loan.IsOverdue(Now.AddTicks(1)));
        Assert.Equal(1, loan.GetOverdueDays(Now.AddTicks(1)));
        Assert.Equal(2, loan.GetOverdueDays(Now.AddDays(1).AddSeconds(1)));
        loan.Return(Now.AddHours(1));
        Assert.False(loan.IsOverdue(Now.AddDays(5)));
        Assert.Equal(1, loan.GetOverdueDays(Now.AddDays(5)));
    }

    [Fact]
    public void DueDateMustBeLaterThanNow()
    {
        var loan = Loan.Create(1, 1, Now.AddDays(-2), Now.AddDays(2));
        Assert.Throws<DomainException>(() => loan.ChangeDueDate(Now, Now));
        loan.ChangeDueDate(Now.AddDays(3), Now);
        Assert.Equal(Now.AddDays(3), loan.DueAtUtc);
    }

    [Fact]
    public void ReaderBlockAndLimitPreventBorrowing()
    {
        var reader = new Reader("R-1", "Читатель", null);
        reader.EnsureCanBorrow(4, 5);
        Assert.Throws<DomainException>(() => reader.EnsureCanBorrow(5, 5));
        reader.Block();
        Assert.Throws<DomainException>(() => reader.EnsureCanBorrow(0, 5));
        reader.Unblock();
        reader.EnsureCanBorrow(0, 5);
        Assert.Throws<DomainException>(() => reader.UpdateContacts("Имя", "not-an-email"));
    }

    [Fact]
    public void ArchiveCannotHideAnActiveLoan()
    {
        var book = new Book("B-1", "Книга", "Автор", "Жанр", 2020);
        Assert.Throws<DomainException>(() => book.EnsureCanBeLoaned(true));
        Assert.Throws<DomainException>(() => book.Archive(true));
        book.Archive(false);
        Assert.Throws<DomainException>(() => book.EnsureCanBeLoaned(false));
        book.Restore();
        book.EnsureCanBeLoaned(false);
        Assert.Throws<DomainException>(() => book.UpdateDetails("", "Автор", "Жанр", 2020));
        Assert.Equal("Книга", book.Title);
    }
}
