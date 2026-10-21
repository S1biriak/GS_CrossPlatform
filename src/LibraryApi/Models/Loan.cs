namespace LibraryApi.Models;

public sealed class Loan
{
    private Loan() { }

    public int Id { get; private set; }
    public int BookId { get; private set; }
    public int ReaderId { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime DueAtUtc { get; private set; }
    public DateTime? ReturnedAtUtc { get; private set; }

    public static Loan Create(int bookId, int readerId, DateTime now, DateTime dueAt)
    {
        EnsureUtc(now);
        EnsureUtc(dueAt);
        if (bookId <= 0 || readerId <= 0) throw DomainException.Invalid("Идентификаторы должны быть положительными.");
        if (dueAt <= now) throw DomainException.Invalid("Срок возврата должен быть позже момента выдачи.");
        return new Loan { BookId = bookId, ReaderId = readerId, IssuedAtUtc = now, DueAtUtc = dueAt };
    }

    public void ChangeDueDate(DateTime dueAt, DateTime now)
    {
        EnsureUtc(now);
        EnsureUtc(dueAt);
        if (ReturnedAtUtc is not null) throw new DomainException("Нельзя менять срок завершённой выдачи.");
        if (dueAt <= now || dueAt <= IssuedAtUtc)
            throw DomainException.Invalid("Новый срок должен быть позже текущего момента и момента выдачи.");
        DueAtUtc = dueAt;
    }

    public void Return(DateTime now)
    {
        EnsureUtc(now);
        if (ReturnedAtUtc is not null) throw new DomainException("Книга уже возвращена.");
        if (now < IssuedAtUtc) throw DomainException.Invalid("Возврат не может предшествовать выдаче.");
        ReturnedAtUtc = now;
    }

    public bool IsOverdue(DateTime now) => ReturnedAtUtc is null && now > DueAtUtc;

    public int GetOverdueDays(DateTime now)
    {
        var end = ReturnedAtUtc ?? now;
        return end <= DueAtUtc ? 0 : (int)Math.Ceiling((end - DueAtUtc).TotalDays);
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw DomainException.Invalid("Внутренние даты должны быть в UTC.");
    }
}
