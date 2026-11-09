using ManaDuitnya.Api.Dtos;
using ManaDuitnya.Api.Entities;
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
public class TransactionController(
    NpgsqlDataSource db
) : ControllerBase
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
        var sql = new StringBuilder(
            """
                SELECT transactions.uuid, transactions.user_id, transactions.wallet_id, transactions.to_wallet_id, 
                    transactions.category_id, transactions.type, transactions.amount, transactions.note, 
                    transactions.transaction_date, categories.name as category_name
                FROM transactions
                INNER JOIN categories ON categories.id = transactions.categoryId
                WHERE user_id = @userId
                AND transaction_date >= @from
                AND transaction_date < @to
            """
        );

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
        if (req.Amount <= 0)
            return BadRequest("Nominal tidak boleh kosong");
        if (req.Type == TransactionType.Transfer) {
            if (req.ToWalletId is null)
                return BadRequest("Transfer harus punya dompet tujuan.");

            if (req.WalletId == req.ToWalletId) 
                return BadRequest("Tidak Bisa Transfer ke dompet yang sama");

            if (req.CategoryId is not null)
                return BadRequest("Tranfer tidak boleh memiliki Kategori");
        } else {
            if (req.CategoryId is null)
                return BadRequest("Kategori wajib diisi");

            if (req.ToWalletId is not null)
                return BadRequest("Dompet tujuan hanya boleh diisi untuk Transfer");
        }

        await using var conn = await db.OpenConnectionAsync();

        var walletIds = new List<long> { req.WalletId };
        if (req.ToWalletId is not null) walletIds.Add(req.ToWalletId.Value);
        walletIds = walletIds.Distinct().ToList();

        var ownedWallets = await conn.ExecuteScalarAsync<int>(
            """
                SELECT COUNT(*)
                FROM wallets
                WHERE user_id = @UserId
                AND id = ANY(@walletIds)
                AND archived_at IS NULL
            """,
            new { UserId, walletIds }
        );

        if (ownedWallets != walletIds.Count) 
            return BadRequest("Dompet tidak Valid atau Sudah diarsipkan");

        if (req.CategoryId is not null) {
            var categoryType = await conn.QueryFirstOrDefaultAsync<short?>(
                """
                    SELECT type 
                    FROM categories 
                    WHERE id = @categoryId 
                    AND user_id = @UserId 
                    AND archived_at IS NULL
                """,
                new { categoryId = req.CategoryId.Value, UserId }
            );
            if (categoryType is null)
                return BadRequest("Kategori tidak valid atau sudah diarsipkan.");

            if (categoryType.Value != (short)req.Type)
                return BadRequest("Tipe kategori harus sesuai dengan tipe Transaksi");
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

        return CreatedAtAction(nameof(GetByUuid), new {uuid = t.Uuid}, t);
    }

    [HttpPut("{uuid:guid}")]
    public async Task<IActionResult> Update(Guid uuid, CreateTransactionRequest req)
    {
        await using var conn = await db.OpenConnectionAsync();

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
    }

    [HttpDelete("{uuid:guid}")]
    public async Task<IActionResult> Delete(Guid uuid)
    {
        await using var conn = await db.OpenConnectionAsync();
        var sql = await conn.ExecuteAsync("""
            DELETE FROM transactions
            WHERE uuid = @uuid
            AND user_id = @UserId
        """,
        new { uuid, UserId });

        return sql > 0 
            ? Ok(new {message = "Berhasil Hapus Transaksi"})
            : NotFound(new {message = "Transaksi tidak ditemukan"});
    }
}