using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LibraryApi.Auth;
using LibraryApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers;

[ApiController, Route("api/v1/auth")]
public sealed class AuthController(AuthService service) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous]
    public async Task<TokenDto> Login(LoginRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await service.Login(request, ct);
    }

    [HttpGet("me"), Authorize]
    public UserDto Me() => new(int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!,
        System.Globalization.CultureInfo.InvariantCulture), User.Identity!.Name!, User.FindFirstValue("role")!);
}
