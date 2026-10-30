using ManaDuitnya.Api.Dtos;
using ManaDuitnya.Api.Services;
using ManaDuitnya.Api.Entities;
using ManaDuitnya.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ManaDuitnya.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserRepository users,
    RefreshTokenRepository refreshTokens,
    TokenService tokens,
    IConfiguration config
) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var email   = req.Email.Trim().ToLowerInvariant();
        var hash    = BCrypt.Net.BCrypt.HashPassword(req.Password);

        try {
            var user = await users.Create(req.Name.Trim(), email, hash);
            return Ok(await IssueTokensAsync(user));
        } catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation) {
            return Conflict("Email sudah terdaftar");
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await users.GetByEmail(req.Email.Trim().ToLowerInvariant());
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash)) {
            return Unauthorized("Email atau password salah!");
        }

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        var userId = await refreshTokens.ConsumeAsync(TokenService.Hash(req.RefreshToken));
        if (userId is null) return Unauthorized("Sesi tidak Valid, silahkan Login ulang!");

        var user = await users.GetById(userId.Value);
        if (user is null) return  Unauthorized();

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        await refreshTokens.RevokeAsync(TokenService.Hash(req.RefreshToken));
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id      = User.FindFirst("sub")?.Value,
        name    = User.FindFirst("name")?.Value,
        email   = User.FindFirst("email")?.Value
    });

    // Membuat sepasang token baru dan menyimpan hash refresh token ke database
    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var (raw, hash) = TokenService.NewRefreshToken();
        var expires = DateTime.UtcNow.AddDays(config.GetValue<int>("Jwt:RefreshDays"));
        await refreshTokens.InsertAsync(user.Id, hash, expires);

        return new AuthResponse
        {
            AccessToken     = tokens.CreateAccessToken(user),
            RefreshToken    = raw,
            UserId          = user.Id,
            Email           = user.Email,
            Name            = user.Name
        };
    }
}