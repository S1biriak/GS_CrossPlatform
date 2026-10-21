using System.Net.Mail;

namespace LibraryApi.Models;

public sealed class Reader
{
    private Reader() { }

    public Reader(string cardNumber, string fullName, string? email)
    {
        CardNumber = Rules.Required(cardNumber, "Номер читательского билета", 40);
        UpdateContacts(fullName, email);
    }

    public int Id { get; private set; }
    public string CardNumber { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string? Email { get; private set; }
    public bool IsBlocked { get; private set; }

    public void UpdateContacts(string fullName, string? email)
    {
        var validName = Rules.Required(fullName, "Имя читателя", 150);
        var validEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (validEmail is not null && (validEmail.Length > 254 || !MailAddress.TryCreate(validEmail, out _)))
            throw DomainException.Invalid("Укажите корректный email.");
        FullName = validName;
        Email = validEmail;
    }

    public void Block() => IsBlocked = true;
    public void Unblock() => IsBlocked = false;

    public void EnsureCanBorrow(int activeLoanCount, int loanLimit)
    {
        if (IsBlocked) throw new DomainException("Читателю запрещены новые выдачи.");
        if (activeLoanCount >= loanLimit)
            throw new DomainException($"Достигнут лимит активных выдач: {loanLimit}.");
    }
}
