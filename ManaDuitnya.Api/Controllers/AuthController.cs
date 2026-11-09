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
            if (user is null) return StatusCode(500, ApiResponse.Fail("Gagal membuat akun"));
            return Ok(ApiResponse<AuthResponse>.Ok(await IssueTokensAsync(user), "Registrasi akun berhasil"));
        } catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation) {
            return Conflict(ApiResponse.Fail("Email sudah terdaftar", "CONFLICT"));
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await users.GetByEmail(req.Email.Trim().ToLowerInvariant());
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash)) {
            return Unauthorized(ApiResponse.Fail("Email atau password salah!", "UNAUTHORIZED"));
        }

        return Ok(ApiResponse<AuthResponse>.Ok(await IssueTokensAsync(user), "Login berhasil"));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        var userId = await refreshTokens.ConsumeAsync(TokenService.Hash(req.RefreshToken));
        if (userId is null) return Unauthorized(ApiResponse.Fail("Sesi tidak valid, silahkan login ulang!", "UNAUTHORIZED"));

        var user = await users.GetById(userId.Value);
        if (user is null) return Unauthorized(ApiResponse.Fail("User tidak ditemukan!", "UNAUTHORIZED"));

        return Ok(ApiResponse<AuthResponse>.Ok(await IssueTokensAsync(user), "Sesi berhasil diperbarui"));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        await refreshTokens.RevokeAsync(TokenService.Hash(req.RefreshToken));
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(ApiResponse<dynamic>.Ok(new
    {
        id      = User.FindFirst("sub")?.Value,
        name    = User.FindFirst("name")?.Value,
        email   = User.FindFirst("email")?.Value
    }, "Data profil berhasil diambil"));

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var (raw, hash) = TokenService.NewRefreshToken();
        var expires = DateTime.UtcNow.AddDays(config.GetValue<int>("Jwt:RefreshDays"));
        await refreshTokens.InsertAsync(user.Id, hash, expires);

        return new AuthResponse
        {
            AccessToken           = tokens.CreateAccessToken(user),
            RefreshToken          = raw,
            UserId                = user.Id,
            Email                 = user.Email,
            Name                  = user.Name,
            RefreshTokenExpiresAt = expires
        };
    }
}