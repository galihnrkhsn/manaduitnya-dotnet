using ManaDuitnya.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using ManaDuitnya.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using Dapper;

namespace ManaDuitnya.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoryController(NpgsqlDataSource db) : ControllerBase
{
    private long UserId => User.GetUserId();
    private enum CategoryType
    {
        Income = 1,
        Expense = 2,
        Transfer = 3
    }
    
    [HttpGet]
    public async Task<IActionResult> List()
    {
        await using var conn = await db.OpenConnectionAsync();
        var rows = await conn.QueryAsync(
            """
                SELECT id, user_id, name, type, color, created_at, updated_at
                FROM categories
                WHERE user_id = @userId
                AND archived_at IS NULL
            """,
            new { UserId }
        );

        var result = rows.Select(r => new
        {
            id = r.id,
            name = r.name,
            type = ((CategoryType)(int)r.type).ToString(),
            color = r.color,
            created_at = r.created_at,
            updated_at = r.updated_at
        });

        return Ok(ApiResponse<IEnumerable<dynamic>>.Ok(result, "Daftar kategori berhasil diambil"));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync(
            """
                SELECT id, user_id, name, type, color, created_at
                FROM categories
                WHERE id = @id
                AND user_id = @userId 
            """,
            new { id, UserId }
        );

        if (row is null) 
            return NotFound(ApiResponse.Fail("Kategori tidak ditemukan", "NOT_FOUND"));

        return Ok(ApiResponse<dynamic>.Ok(new
        {
            id = row.id,
            name = row.name,
            type = row.type,
            color = row.color,
            created_at = row.created_at
        }, "Detail kategori berhasil diambil"));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryDtos req)
    {
        await using var conn = await db.OpenConnectionAsync();

        var id = await conn.QueryFirstOrDefaultAsync<long?>(
            """
                INSERT INTO categories
                    (user_id, name, type, color, created_at)
                VALUES
                    (@userId, @name, @type, @color, now())
                RETURNING id
            """,
            new { UserId, req.Name, req.Type, req.Color }
        );

        if (id is null) 
            return BadRequest(ApiResponse.Fail("Gagal bikin kategori. Servernya aja lelah nyatet rencana keborosanmu.", "BAD_REQUEST"));

        return Created($"/api/categories/{id}", ApiResponse<object>.Ok(new { id }, "Kategori berhasil dibuat"));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, CategoryDtos req)
    {
        await using var conn = await db.OpenConnectionAsync();

        var sql = await conn.ExecuteAsync(
            """
                UPDATE categories
                SET name = @Name,
                    color = @Color
                WHERE user_id = @userId
                AND id = @id
            """,
            new { req.Name, req.Color, UserId, id }
        );

        return sql > 0
            ? Ok(ApiResponse.Ok("Sip, detail kategori sudah diperbarui. Biar lebih presisi nyatet jalur kemiskinannya."))
            : NotFound(ApiResponse.Fail("Kategori nggak ketemu. Jangan ngadi-ngadi nyari pengeluaran fiktif.", "NOT_FOUND"));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        var inUse = await conn.ExecuteScalarAsync<bool>(
            """
                SELECT EXISTS (
                    SELECT 1 FROM transactions
                    WHERE category_id = @id AND user_id = @userId
                )
            """,
            new { id, UserId }
        );

        if (inUse) return BadRequest(ApiResponse.Fail(
            "Eits, gagal archive! Mau ngilangin barang bukti keborosan ya? Kosongin/pindah dulu transaksinya.", 
            "CATEGORY_IN_USE"
        ));

        var archived = await conn.ExecuteAsync(
            """
                UPDATE categories SET archived_at = now(), updated_at = now()
                WHERE id = @id 
                AND user_id = @userId
                AND archived_at IS NULL
            """,
            new { id, UserId }
        );
        return archived > 0
            ? Ok(ApiResponse.Ok("Kategori sukses diarsipkan. Satu jalur keborosan resmi ditutup."))
            : NotFound(ApiResponse.Fail("Kategori nggak ketemu. Jangan ngadi-ngadi nyari pengeluaran fiktif!", "NOT_FOUND"));
    }

    [HttpPut("{id:long}/unarchive")]
    public async Task<IActionResult> UnArchive(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        var sql = await conn.ExecuteAsync("""
            UPDATE categories
            SET archived_at = NULL
            WHERE user_id = @UserId
            AND id = @id
        """, new {UserId, id});

        return sql > 0
            ? Ok(ApiResponse.Ok("Kategori diaktifkan kembali. Nggak jadi tobat jajan ini ya?"))
            : NotFound(ApiResponse.Fail("Kategori nggak ketemu. Jangan ngadi-ngadi nyari pengeluaran fiktif.", "NOT_FOUND"));
    }
}
