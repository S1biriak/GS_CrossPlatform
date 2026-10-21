using System.ComponentModel.DataAnnotations;

namespace LibraryApi.Options;

public sealed class JwtOptions
{
    [Required, MinLength(64)] public string Key { get; set; } = "";
    [Required] public string Issuer { get; set; } = "LibraryApi";
    [Required] public string Audience { get; set; } = "LibraryApi.Client";
    [Range(1, 120)] public int LifetimeMinutes { get; set; } = 30;
}

public sealed class LibraryOptions
{
    [Range(1, 100)] public int ActiveLoanLimit { get; set; } = 5;
    [Range(1, 365)] public int DefaultLoanDays { get; set; } = 14;
}

public sealed class DemoOptions
{
    public bool Seed { get; set; }
    public string? AdminPassword { get; set; }
    public string? UserPassword { get; set; }
    public bool Swagger { get; set; }
}
