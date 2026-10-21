namespace LibraryApi.Models;

/// <summary>Один физический экземпляр книги, а не название произведения.</summary>
public sealed class Book
{
    private Book() { }

    public Book(string inventoryNumber, string title, string author, string genre, int publicationYear)
    {
        InventoryNumber = Rules.Required(inventoryNumber, "Инвентарный номер", 40);
        UpdateDetails(title, author, genre, publicationYear);
    }

    public int Id { get; private set; }
    public string InventoryNumber { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Author { get; private set; } = "";
    public string Genre { get; private set; } = "";
    public int PublicationYear { get; private set; }
    public bool IsArchived { get; private set; }

    public void UpdateDetails(string title, string author, string genre, int publicationYear)
    {
        var validTitle = Rules.Required(title, "Название", 200);
        var validAuthor = Rules.Required(author, "Автор", 150);
        var validGenre = Rules.Required(genre, "Жанр", 80);
        if (publicationYear is < 1 or > 9999)
            throw DomainException.Invalid("Год издания должен быть от 1 до 9999.");
        Title = validTitle;
        Author = validAuthor;
        Genre = validGenre;
        PublicationYear = publicationYear;
    }

    public void EnsureCanBeLoaned(bool hasActiveLoan)
    {
        if (IsArchived) throw new DomainException("Архивный экземпляр нельзя выдать.");
        if (hasActiveLoan) throw new DomainException("Экземпляр уже выдан другому читателю.");
    }

    public void Archive(bool hasActiveLoan)
    {
        if (hasActiveLoan) throw new DomainException("Сначала необходимо вернуть выданный экземпляр.");
        IsArchived = true;
    }

    public void Restore() => IsArchived = false;
}
