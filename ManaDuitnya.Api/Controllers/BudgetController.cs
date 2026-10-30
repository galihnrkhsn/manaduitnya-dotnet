using ManaDuitnya.Api.Dtos;
using ManaDuitnya.Api.Entities;
using ManaDuitnya.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using ManaDuitnya.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using Dapper;

namespace ManaDuitnya.Api.Controllers;
[ApiController]
[Authorize]
[Route("api/budget")]
public class BudgetController(NpgsqlDataSource db) : ControllerBase
{
    private long UserId => User.GetUserId();
    private enum CategoryType
    {
        Income = 1,
        Expense = 2
    }

    [HttpGet]
    public async Task<IActionResult> GetByMonth([FromQuery] DateOnly month)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync(
            """
                SELECT b.id, b.user_id, b.category_id, b.month, b.amount, b.created_at, b.updated_at,
                    c.name as category_name, c.type as category_type, c.color as category_color
                FROM budgets b
                INNER JOIN categories c ON c.id = b.category_id
                WHERE c.archived_at IS NULL
                AND b.month = @month AND b.user_id = @userId
            """,
            new { month, UserId }
        );

        if (row is null) return NotFound();

        return Ok(new
        {
            id = row.id,
            userId = row.user_id,
            categoryId = row.category_id,
            month = row.month,
            amount = row.amount,
            createdAt = row.created_at,
            updatedAt = row.updated_at,
            category_name = row.category_name,
            category_type = ((CategoryType)(int)row.category_type).ToString()
        });
    }

    public async Task<IActionResult> Create(BudgetDtos req)
    {
        await using var conn = await db.OpenConnectionAsync();

        try
        {
            var id = await conn.QueryFirstOrDefaultAsync<long?>(
                """
                    INSERT INTO budgets 
                        (user_id, category_id, month, amount, created_at, updated_at)
                    VALUES
                        (@userId, @categoryId, @month, @amoung, now(), now())
                """,
                new { UserId, req.CategoryId, req.Month, req.Amount }
            );

            if (id is null) return BadRequest(new {message = "Budget"});
            return Created($"/budgets/{id}", new { id });
        } catch (PostgresException e) when (e.SqlState == "23505")
        {
            return Conflict(new {message = "Budget Untuk Kategori dan bulan ini sudah ada"});
        }
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync(
            """
                SELECT id, user_id, category_id, month, amount, created_at
                FROM budgets
                WHERE id = @id
                AND user_id = @userId
            """,
            new { id, UserId }
        );

        if (row is null) return BadRequest(new {message = "Budget tidak ditemukan"});

        return Ok(new
        {
            id = row.id,
            userId = row.user_id,
            categoryId = row.category_id,
            month = row.month,
            amount = row.amount,
            createdAt = row.created_at,
        });
    }
}