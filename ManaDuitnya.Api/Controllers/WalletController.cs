using ManaDuitnya.Api.Dtos;
using ManaDuitnya.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using ManaDuitnya.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using Dapper;

namespace ManaDuitnya.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/wallets")]
public class WalletController(NpgsqlDataSource db) : ControllerBase
{
    private long UserId => User.GetUserId();
    private const string SelectWallet = "SELECT id, user_id, name, initial_balance, archived_at, created_at, updated_at FROM wallets";

    [HttpGet]
    public async Task<IActionResult> List()
    {
        await using var conn = await db.OpenConnectionAsync();
        var rows = await conn.QueryAsync(
            $"{SelectWallet} WHERE archived_at IS NULL AND user_id = @UserId ORDER BY created_at desc",
            new { UserId }
        );

        return Ok(ApiResponse<IEnumerable<dynamic>>.Ok(rows, "Daftar dompet berhasil diambil"));
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateWallet(WalletDtos req) 
    {
        var wallet = new Wallet
        {
            UserId = UserId,
            Name = req.Name.Trim(),
            InitialBalance = req.InitialBalance
        };

        const string sql = """
            INSERT INTO wallets
                (user_id, name, initial_balance, created_at, updated_at)
            VALUES
                (@userId, @name, @initialBalance, now(), now())
            RETURNING id
        """;

        await using var conn = await db.OpenConnectionAsync();
        wallet.Id = await conn.ExecuteScalarAsync<long>(sql, wallet);

        return CreatedAtAction(nameof(GetById), new { id = wallet.Id }, ApiResponse<Wallet>.Ok(wallet, "Dompet berhasil dibuat"));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QuerySingleOrDefaultAsync($"{SelectWallet} WHERE id = @id AND user_id = @userId", new { id, UserId });
        return row is null 
            ? NotFound(ApiResponse.Fail("Dompet tidak ditemukan", "NOT_FOUND")) 
            : Ok(ApiResponse<dynamic>.Ok(row, "Detail dompet berhasil diambil"));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, WalletDtos req)
    {
        var name = req.Name.Trim();

        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.ExecuteAsync ("""
            UPDATE wallets
            SET name = @name,
                initial_balance = @InitialBalance
            WHERE id = @id
            AND user_id = @userId
        """,
        new { name, req.InitialBalance, id, UserId }
        );

        return row > 0
            ? Ok(ApiResponse.Ok("Update dompet sukses! Tampilannya aja yang baru, isinya tetep segitu-gitu aja."))
            : NotFound(ApiResponse.Fail("Waduh, dompet gaib! Nggak ketemu di sistem.", "NOT_FOUND"));
    }

    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived()
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryAsync($"""
            {SelectWallet} 
            WHERE archived_at IS NOT NULL 
            AND user_id = @UserId 
            ORDER BY archived_at DESC
        """,
        new {UserId});

        return Ok(ApiResponse<IEnumerable<dynamic>>.Ok(row, "Daftar dompet terarsip berhasil diambil"));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id)
    {
        await using var conn = await db.OpenConnectionAsync();

        var sql = await conn.ExecuteAsync(
            """
                UPDATE wallets
                SET archived_at = now(), updated_at = now()
                WHERE id = @id AND user_id = @userId AND archived_at IS NULL
            """, 
            new { id, UserId }
        );

        return sql > 0
            ? Ok(ApiResponse.Ok("Dompet masuk arsip. Istirahat dulu dia, capek dikuras terus."))
            : NotFound(ApiResponse.Fail("Waduh, dompet gaib! Nggak ketemu di sistem.", "NOT_FOUND"));
    }

    [HttpPut("unarchive/{id:long}")]
    public async Task<IActionResult> UnArchive(long id)
    {
        await using var conn = await db.OpenConnectionAsync();

        var sql = await conn.ExecuteAsync("""
            UPDATE wallets
            SET archived_at = NULL
            WHERE id = @id
            AND user_id = @UserId
        """, new {id, UserId});

        return sql > 0
            ? Ok(ApiResponse.Ok("Dompet kembali aktif! Siap dikuras lagi."))
            : NotFound(ApiResponse.Fail("Waduh, dompet gaib! Nggak ketemu di sistem.", "NOT_FOUND"));
    }
}