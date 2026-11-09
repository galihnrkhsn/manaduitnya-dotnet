using ManaDuitnya.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using ManaDuitnya.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using Dapper;

namespace ManaDuitnya.Api.Controllers;
[ApiController]
[Authorize]
[Route("api/budgets")]
public class BudgetController(NpgsqlDataSource db) : ControllerBase
{
    private long UserId => User.GetUserId();
    private enum CategoryType
    {
        Income = 1,
        Expense = 2
    }

    private const string SelectBudget = """
        SELECT b.uuid, b.user_id, b.category_id, b.month, b.amount, b.created_at, b.updated_at,
            c.name as category_name, c.type as category_type, c.color as category_color
        FROM budgets b
        INNER JOIN categories c ON c.id = b.category_id
        WHERE c.archived_at IS NULL
        AND c.type = 2
        AND b.user_id = @UserId
    """;

    [HttpGet]
    public async Task<IActionResult> GetByMonth([FromQuery] DateOnly? month)
    {
        var now             = DateTime.Today;
        var Month           = month ?? new DateOnly(now.Year, now.Month, 1);
        var MonthDateTime   = Month.ToDateTime(TimeOnly.MinValue);

        await using var conn = await db.OpenConnectionAsync();
        var rows = await conn.QueryAsync(
            $"""
                {SelectBudget}
                AND b.month = @Month 
                ORDER BY c.name ASC
            """,
            new { Month, UserId }
        );

        var result = rows.Select(row => new
        {
            uuid            = row.uuid,
            userId          = row.user_id,
            categoryId      = row.category_id,
            month           = row.month,
            amount          = row.amount,
            createdAt       = row.created_at,
            updatedAt       = row.updated_at,
            category_name   = row.category_name,
            category_type   = ((CategoryType)(int)row.category_type).ToString(),
            category_color  = row.category_color 
        });

        return Ok(ApiResponse<IEnumerable<dynamic>>.Ok(result, "Daftar anggaran berhasil diambil"));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(BudgetDtos req)
    {
        var normalizedMonth = new DateOnly(req.Month.Year, req.Month.Month, 1);

        await using var conn = await db.OpenConnectionAsync();

        var isCategoryValid = await conn.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM categories 
                WHERE id = @CategoryId 
                AND user_id = @UserId 
                AND type = 2
                AND archived_at IS NULL
            )
        """, new {req.CategoryId, UserId});

        if (!isCategoryValid) 
            return BadRequest(ApiResponse.Fail("Kategori nggak ketemu.", "VALIDATION_ERROR"));

        const string sql = """
            INSERT INTO budgets
                (user_id, category_id, month, amount, created_at)
            VALUES
                (@UserId, @CategoryId, @Month, @Amount, NOW())
            ON CONFLICT (user_id, category_id, month)
            DO UPDATE SET
                amount = EXCLUDED.amount,
                updated_at = now();
        """;

        var row = await conn.ExecuteAsync(sql, new
        {
            UserId,
            CategoryId = req.CategoryId,
            Month = normalizedMonth,
            Amount = req.Amount
        });

        return row > 0
            ? Ok(ApiResponse.Ok("Anggaran resmi ditetapkan! Mari kita lihat seberapa cepat nominal ini akan jebol."))
            : NotFound(ApiResponse.Fail("Waduh, anggaran gaib! Nggak ada di sistem.", "NOT_FOUND"));
    }

    [HttpGet("{uuid:Guid}")]
    public async Task<IActionResult> GetByUuid(Guid uuid)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync(
            $"""
                {SelectBudget}
                AND b.uuid = @uuid
            """,
            new { UserId, uuid }
        );

        if (row is null)
            return NotFound(ApiResponse.Fail("Waduh, anggaran gaib! Nggak ada di sistem.", "NOT_FOUND"));

        var result = new
        {
            id              = row.uuid,
            userId          = row.user_id,
            categoryId      = row.category_id,
            month           = row.month,
            amount          = row.amount,
            createdAt       = row.created_at,
            updatedAt       = row.updated_at,
            category_name   = row.category_name,
            category_type   = ((CategoryType)(int)row.category_type).ToString(),
            category_color  = row.category_color 
        };

        return Ok(ApiResponse<dynamic>.Ok(result, "Detail anggaran berhasil diambil"));
    }

    [HttpDelete("{uuid:Guid}")]
    public async Task<IActionResult> Delete(Guid uuid)
    {
        await using var conn = await db.OpenConnectionAsync();

        var sql = await conn.ExecuteAsync(
            """
                DELETE FROM budgets
                WHERE user_id = @UserId
                AND id = @uuid
            """,
            new {UserId, uuid}
        );

        return sql > 0
            ? Ok(ApiResponse.Ok("Anggaran dihapus! Anggap aja bulan ini kita hidup tanpa rem."))
            : NotFound(ApiResponse.Fail("Waduh, anggaran gaib! Nggak ada di sistem.", "NOT_FOUND"));        
    }

    [HttpPost("copy")]
    public async Task<IActionResult> Copy([FromBody] CopyBudgetRequest req)
    {
        var fromMonth   = new DateOnly(req.FromMonth.Year, req.FromMonth.Month, 1);
        var toMonth     = new DateOnly(req.ToMonth.Year, req.ToMonth.Month, 1);

        if (fromMonth == toMonth)
            return BadRequest(ApiResponse.Fail("Bulan asal dan bulan tujuan tidak boleh sama", "VALIDATION_ERROR"));

        await using var conn = await db.OpenConnectionAsync();
        const string sql = """
            INSERT INTO budgets
                (user_id, category_id, month, amount, created_at)
            SELECT user_id, category_id, @toMonth, amount, now()
            FROM budgets
            WHERE user_id = @UserId
            AND month = @fromMonth
            ON CONFLICT (user_id, category_id, month) DO NOTHING;
        """;

        var copiedRows = await conn.ExecuteAsync(sql, new {UserId, fromMonth, toMonth});

        if (copiedRows == 0) {
            return Ok(ApiResponse.Ok("Tidak ada anggaran dari bulan lalu yang dapat disalin"));
        }

        return Ok(ApiResponse.Ok($"Berhasil copy {copiedRows} anggaran. Semoga jebolnya nggak ikut ke-copy ya."));
    }
}