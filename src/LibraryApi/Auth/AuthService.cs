using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Models;
using LibraryApi.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LibraryApi.Auth;

public sealed class AuthService(LibraryDbContext db, IPasswordHasher<AppUser> hasher,
    IOptions<JwtOptions> options, TimeProvider clock)
{
    // A dummy hash makes unknown-login requests perform a password hash check too.
    private static readonly AppUser Dummy = new() { Login = "unknown" };
    private static readonly string DummyHash = new PasswordHasher<AppUser>().HashPassword(Dummy, Guid.NewGuid().ToString());

    public async Task<TokenDto> Login(LoginRequest request, CancellationToken ct)
    {
        var login = request.Login.Trim();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Login == login, ct);
        var result = hasher.VerifyHashedPassword(user ?? Dummy, user?.PasswordHash ?? DummyHash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
            throw new DomainException("Неверный логин или пароль.", 401);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Login),
            new Claim("role", user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, new(user.Id, user.Login, user.Role));
    }
}
