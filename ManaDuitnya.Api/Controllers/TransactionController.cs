using ManaDuitnya.Api.Dtos;
using ManaDuitnya.Api.Entities;
using ManaDuitnya.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using ManaDuitnya.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using System.Text;
using Dapper;

namespace ManaDuitnya.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public class TransactionController(NpgsqlDataSource db) : ControllerBase
{
    private long UserId => User.GetUserId();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime from, 
        [FromQuery] DateTime to, 
        [FromQuery] long? walletId, 
        [FromQuery] long? categoryId, 
        [FromQuery] TransactionType? type, 
        [FromQuery] string? search
    ) {
        var sql = new StringBuilder("""
            SELECT uuid, user_id, wallet_id, to_wallet_id, category_id,
                type, amount, note, transaction_date
            FROM transactions
            WHERE user_id = @userId
            AND transaction_date >= @from
            AND transaction_date < @to
            """);

        var p = new DynamicParameters();
        p.Add("userId", UserId);
        p.Add("from", from.Date);
        p.Add("to", to.Date.AddDays(1));

        if (walletId is not null)
        {
            sql.AppendLine(" AND wallet_id = @walletId");
            p.Add("walletId", walletId);
        }
        if (categoryId is not null)
        {
            sql.AppendLine(" AND category_id = @categoryId");
            p.Add("categoryId", categoryId);
        }
        if (type is not null)
        {
            sql.AppendLine(" AND type = @type");
            p.Add("type", (int)type);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql.AppendLine(" AND note ILIKE @search");
            p.Add("search", $"%{search.Trim()}%");
        }

        sql.AppendLine(" ORDER BY transaction_date DESC");

        await using var conn = await db.OpenConnectionAsync();
        var list = await conn.QueryAsync<Transaction>(sql.ToString(), p);

        return Ok(list);
    } 

    [HttpGet("{uuid:guid}")]
    public async Task<IActionResult> GetByUuid(Guid uuid)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync("""
                SELECT uuid, user_id, wallet_id, to_wallet_id, category_id,
                    type, amount, note, transaction_date, created_at, updated_at
                FROM transactions
                WHERE uuid = @uuid
                AND user_id = @userId
            """, 
            new { uuid, UserId }
        );

        if (row is null) return NotFound();

        return Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTransactionRequest req)
    {
        if (req.Type == TransactionType.Transfer && req.ToWalletId is null)
            return BadRequest("Transfer harus punya dompet tujuan.");
        await using var conn = await db.OpenConnectionAsync();

        var walletIds = new List<long> { req.WalletId };
        if (req.ToWalletId is not null) walletIds.Add(req.ToWalletId.Value);
        walletIds = walletIds.Distinct().ToList();

        var ownedWallets = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM wallets WHERE user_id = @userId, AND id = ANY(@ids)",
            new { UserId, walletIds }
        );

        if (ownedWallets != walletIds.Count) return BadRequest("Dompet tidak Valid");

        if (req.CategoryId is not null) {
            var categoryOk = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXIST (SELECT 1 FROM categories WHERE id = @id AND user_id = @userId)",
                new { id = req.CategoryId.Value, userId = UserId }
            );

            if (!categoryOk) return BadRequest("Kategori Tidak Valid");
        }

        var t = new Transaction
        {
            Uuid            = Guid.NewGuid(),
            UserId          = UserId,
            WalletId        = req.WalletId,
            ToWalletId      = req.ToWalletId,
            CategoryId      = req.CategoryId,
            Type            = req.Type,
            Amount          = req.Amount,
            Note            = req.Note,
            TransactionDate = req.TransactionDate
        };

        const string sql = """
            INSERT INTO transactions
                (uuid, user_id, wallet_id, to_wallet_id, category_id, type, amount, note, transaction_date, created_at)
            VALUES
                (@Uuid, @UserId, @WalletId, @ToWalletId, @CategoryId, @Type, @Amount, @Note, @TransactionDate, now())
        """;


        await conn.ExecuteAsync(sql, new
        {
            t.Uuid, t.UserId, t.WalletId, t.ToWalletId, t.CategoryId, Type = (short)t.Type, t.Amount, t.Note, t.TransactionDate
        });

        return Created($"api/transactions/{t.Uuid}", t);
    }

    [HttpPut("{uuid:guid}")]
    public async Task<IActionResult> Update(Guid uuid, CreateTransactionRequest req)
    {
        await using var conn = await db.OpenConnectionAsync();

        try {
            var sql = await conn.ExecuteAsync(
                """
                    UPDATE transactions 
                    SET wallet_id = @WalletId,
                        to_wallet_id = @ToWalletId,
                        category_id = @CategoryId,
                        type = @Type,
                        amount = @Amount,
                        note = @Note,
                        transaction_date = @TransactionDate
                    WHERE uuid = @uuid
                    AND user_id = @UserId
                """,
                new { req.WalletId, req.ToWalletId, req.CategoryId, req.Type, req.Amount, req.Note, req.TransactionDate, uuid, UserId }
            );

            return sql > 0
                ? Ok(new {message = "Berhasil ubah Transaksi"})
                : NotFound(new {message = "Transaksi tidak ditemukan!"});
        } catch (PostgresException e) {
            return Conflict(new {e.Message});
        }
    }

    [HttpDelete("{uuid:guid}")]
    public async Task<IActionResult> Delete(Guid uuid)
    {
        await using var conn = await db.OpenConnectionAsync();
        try {
            var sql = await conn.ExecuteAsync("""
                DELETE FROM transactions
                WHERE uuid = @uuid
                AND user_id = @UserId
            """,
            new { uuid, UserId });

            return sql > 0 
                ? Ok(new {message = "Berhasil Hapus Transaksi"})
                : NotFound(new {message = "Transaksi tidak ditemukan"});
        } catch (PostgresException e) {
            return Conflict(new {e.Message});
        }
    }
}