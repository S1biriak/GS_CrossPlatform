namespace LibraryApi.Auth;

/// <summary>Техническая учётная запись API; не является читателем библиотеки.</summary>
public sealed class AppUser
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.User;
}

public static class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";
}
