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
        [FromQuery] DateTime? from, 
        [FromQuery] DateTime? to, 
        [FromQuery] long? walletId, 
        [FromQuery] long? categoryId, 
        [FromQuery] TransactionType? type, 
        [FromQuery] string? search
    ) {
        var sql = new StringBuilder(
            """
                SELECT 
                    transactions.uuid, 
                    transactions.user_id, 
                    transactions.wallet_id, 
                    transactions.to_wallet_id, 
                    transactions.category_id, 
                    transactions.type, 
                    transactions.amount, 
                    transactions.note, 
                    transactions.transaction_date, 
                    transactions.created_at,
                    transactions.updated_at,
                    categories.name AS category_name, 
                    categories.color AS category_color
                FROM transactions
                LEFT JOIN categories ON categories.id = transactions.category_id
                WHERE transactions.user_id = @userId
            """
        );

        var p = new DynamicParameters();
        p.Add("userId", UserId);

        if (from is not null)
        {
            sql.AppendLine(" AND transactions.transaction_date >= @from");
            p.Add("from", from.Value.Date);
        }

        if (to is not null)
        {
            sql.AppendLine(" AND transactions.transaction_date < @to");
            p.Add("to", to.Value.Date.AddDays(1));
        }

        if (walletId is not null)
        {
            sql.AppendLine(" AND transactions.wallet_id = @walletId");
            p.Add("walletId", walletId);
        }
        if (categoryId is not null)
        {
            sql.AppendLine(" AND transactions.category_id = @categoryId");
            p.Add("categoryId", categoryId);
        }
        if (type is not null)
        {
            sql.AppendLine(" AND transactions.type = @type");
            p.Add("type", (short)type);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql.AppendLine(" AND transactions.note ILIKE @search");
            p.Add("search", $"%{search.Trim()}%");
        }

        sql.AppendLine(" ORDER BY transactions.transaction_date DESC");

        await using var conn = await db.OpenConnectionAsync();
        var list = await conn.QueryAsync<TransactionResponse>(sql.ToString(), p);

        return Ok(ApiResponse<IEnumerable<TransactionResponse>>.Ok(list, "Daftar transaksi berhasil diambil"));
    } 

    [HttpGet("{uuid:guid}")]
    public async Task<IActionResult> GetByUuid(Guid uuid)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync<TransactionResponse>("""
                SELECT 
                    transactions.uuid, 
                    transactions.user_id, 
                    transactions.wallet_id, 
                    transactions.to_wallet_id, 
                    transactions.category_id,
                    transactions.type, 
                    transactions.amount, 
                    transactions.note, 
                    transactions.transaction_date, 
                    transactions.created_at, 
                    transactions.updated_at,
                    categories.name AS category_name, 
                    categories.color AS category_color
                FROM transactions
                LEFT JOIN categories ON categories.id = transactions.category_id
                WHERE transactions.uuid = @uuid
                AND transactions.user_id = @userId
            """, 
            new { uuid, UserId }
        );

        if (row is null) return NotFound(ApiResponse.Fail("Transaksi tidak ditemukan"));

        return Ok(ApiResponse<TransactionResponse>.Ok(row));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTransactionRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(ApiResponse.Fail("Nominal tidak boleh kosong", "VALIDATION_ERROR"));
        if (req.Type == TransactionType.Transfer) {
            if (req.ToWalletId is null)
                return BadRequest(ApiResponse.Fail("Transfer harus punya dompet tujuan.", "VALIDATION_ERROR"));

            if (req.WalletId == req.ToWalletId) 
                return BadRequest(ApiResponse.Fail("Tidak Bisa Transfer ke dompet yang sama", "VALIDATION_ERROR"));

            if (req.CategoryId is not null)
                return BadRequest(ApiResponse.Fail("Transfer tidak boleh memiliki Kategori", "VALIDATION_ERROR"));
        } else {
            if (req.CategoryId is null)
                return BadRequest(ApiResponse.Fail("Kategori wajib diisi", "VALIDATION_ERROR"));

            if (req.ToWalletId is not null)
                return BadRequest(ApiResponse.Fail("Dompet tujuan hanya boleh diisi untuk Transfer", "VALIDATION_ERROR"));
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
            return BadRequest(ApiResponse.Fail("Dompet tidak Valid atau Sudah diarsipkan", "VALIDATION_ERROR"));

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
                return BadRequest(ApiResponse.Fail("Kategori tidak valid atau sudah diarsipkan.", "VALIDATION_ERROR"));

            if (categoryType.Value != (short)req.Type)
                return BadRequest(ApiResponse.Fail("Tipe kategori harus sesuai dengan tipe Transaksi", "VALIDATION_ERROR"));
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

        await TouchUserSessionAsync(conn, UserId);

        return CreatedAtAction(nameof(GetByUuid), new {uuid = t.Uuid}, ApiResponse<Transaction>.Ok(t, "Transaksi berhasil dibuat"));
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

        if (sql > 0)
        {
            await TouchUserSessionAsync(conn, UserId);
            return Ok(ApiResponse.Ok("Berhasil ubah Transaksi"));
        }

        return NotFound(ApiResponse.Fail("Transaksi tidak ditemukan!"));
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

        if (sql > 0)
        {
            await TouchUserSessionAsync(conn, UserId);
            return Ok(ApiResponse.Ok("Berhasil Hapus Transaksi"));
        }

        return NotFound(ApiResponse.Fail("Transaksi tidak ditemukan"));
    }

    private static async Task TouchUserSessionAsync(NpgsqlConnection conn, long userId)
    {
        await conn.ExecuteAsync("""
            UPDATE refresh_tokens
            SET expires_at = now() + interval '90 days'
            WHERE user_id = @userId
            AND revoked_at IS NULL
            AND expires_at > now()
        """, new { userId });
    }
}