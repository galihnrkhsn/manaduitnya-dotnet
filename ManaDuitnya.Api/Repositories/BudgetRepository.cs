using System.Data;
using Dapper;
using ManaDuitnya.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ManaDuitnya.Api.Repositories;

public class BudgetRepository(NpgsqlDataSource db)
{
    private const string SelectBudget = """
        SELECT b.id, b.user_id, b.category_id, b.month, b.amount, b.created_at, b.updated_at,
             c.name as category_name, c.type as category_type, c.color as category_color
        FROM budgets b
        INNER JOIN categories c ON c.id = b.category_id
        WHERE c.archived_at IS NULL
        """;

    public enum CategoryType
    {
        Income = 1,
        Expense = 2
    }

    public async Task<Object?> GetByMonth(DateOnly month, long userId)
    {
        await using var conn = await db.OpenConnectionAsync();
        var result = await conn.QueryFirstOrDefaultAsync(
            $"""
            {SelectBudget}
            AND b.month = @month AND b.user_id = @userId
            """,
            new { month, userId }
        );

        if (result is null) return null;

        return new {
            id              = result.id,
            userId          = result.user_id,
            categoryId      = result.category_id,
            month           = result.month,
            amount          = result.amount,
            createdAt       = result.created_at,
            updatedAt       = result.updated_at,
            category_name   = result.category_name,
            category_type   = ((CategoryType)(int)result.category_type).ToString(),
            category_color  = result.category_color
        };
    }
}