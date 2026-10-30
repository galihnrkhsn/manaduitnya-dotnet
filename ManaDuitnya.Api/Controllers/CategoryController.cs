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
                SELECT id, user_id, name,type, color, created_at, updated_at
                FROM categories
                WHERE user_id = @userId
                AND archived_at IS NULL
            """,
            new { UserId }
        );

        return Ok(rows.Select(r => new
        {
            id = r.id,
            name = r.name,
            type = ((CategoryType)(int)r.type).ToString(),
            color = r.color,
            created_at = r.created_at,
            updated_at = r.updated_at
        }));
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

        if (row is null) return NotFound();

        return Ok(new
        {
            id = row.id,
            name = row.name,
            type = row.type,
            color = row.color,
            created_at = row.created_at
        });
    }

    public async Task<IActionResult> Create(CategoryDtos req)
    {
        await using var conn = await db.OpenConnectionAsync();

        try
        {
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
            if (id is null) return BadRequest(new {message = "Error"});
            return Created($"/categories/{id}", new {id});
        } catch (PostgresException e) when (e.SqlState == "23505")
        {
            return Conflict(new { message = "Kategori sudah ada" });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, CategoryDtos req)
    {
        await using var conn = await db.OpenConnectionAsync();

        try
        {
            var sql = await conn.ExecuteAsync(
                """
                    UPDATE categories
                    SET name = @Name,
                        type = @Type,
                        color = @Color
                    WHERE user_id = @userId
                    AND id = @id
                """,
                new { req.Name, req.Type, req.Color, UserId, id }
            );

            return sql > 0
                ? Ok(new { message = "Kategori berhasil diubah." })
                : NotFound(new { message = "Kategori tidak ditemukan!" });
        } catch (PostgresException e)
        {
            return Conflict(new {message = e.Message});
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        try
        {
            var inUse = await conn.ExecuteScalarAsync<bool>(
                """
                    SELECT EXISTS (
                        SELECT 1 FROM transactions
                        WHERE category_id = @id AND user_id = @userId
                    )
                """,
                new { id, UserId }
            );

            if (inUse) return BadRequest(new {message = "Kategori masih digunakan pada transaksi"});

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
                ? Ok(new { message = "Kategori berhasil dihapus." })
                : NotFound(new { message = "Kategori tidak ditemukan!" });
        } catch (PostgresException e)
        {
            return Conflict(new {e.Message});
        }
    }
}
