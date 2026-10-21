namespace LibraryApi.Models;

/// <summary>Ожидаемая ошибка правила предметной области.</summary>
public sealed class DomainException(string message, int statusCode = 409) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static DomainException NotFound(string resource) => new($"{resource} не найден(а).", 404);
    public static DomainException Invalid(string message) => new(message, 400);
}

internal static class Rules
{
    public static string Required(string? value, string name, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength)
            throw DomainException.Invalid($"{name}: требуется от 1 до {maxLength} символов.");
        return text;
    }
}
