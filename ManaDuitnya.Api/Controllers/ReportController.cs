using ManaDuitnya.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using ManaDuitnya.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using Dapper;

namespace ManaDuitnya.Api.Controllers;
[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportController(NpgsqlDataSource db) : ControllerBase
{
    private long UserId => User.GetUserId();
    private enum CategoryType
    {
        Income = 1,
        Expense = 2,
        Transfer = 3
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] FromToRequest req)
    {
        var now             = DateTime.Today;
        var from            = req.From ?? new DateOnly(now.Year, now.Month, 1);
        var to              = req.To ?? new DateOnly(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
        var fromDateTime    = from.ToDateTime(TimeOnly.MinValue);
        var toDateTimeExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        await using var conn = await db.OpenConnectionAsync();

        const string summarySql = """
            SELECT 
                COALESCE(SUM(amount) FILTER (WHERE type = 1), 0) AS total_income,
                COALESCE(SUM(amount) FILTER (WHERE type = 2), 0) AS total_expense
            FROM transactions
            WHERE user_id = @UserId
            AND transaction_date >= @fromDateTime
            AND transaction_date < @toDateTimeExclusive
        """;
        var summary = await conn.QueryFirstAsync(summarySql, new
        {
            UserId,
            fromDateTime,
            toDateTimeExclusive
        });
        long totalIncome    = (long)summary.total_income;
        long totalExpense   = (long)summary.total_expense;
        long netFlow        = totalIncome - totalExpense;

        const string balanceSql = """
            SELECT
            COALESCE(SUM(w.initial_balance), 0) +
            COALESCE(SUM(
                CASE
                    WHEN t.type = 1 AND t.wallet_id = w.id THEN t.amount
                    WHEN t.type = 2 AND t.wallet_id = w.id THEN -t.amount
                    WHEN t.type = 3 AND t.to_wallet_id = w.id THEN t.amount
                    WHEN t.type = 3 AND t.wallet_id = w.id THEN -t.amount
                    ELSE 0
                END
            ), 0) AS total_balance
            FROM wallets w
            LEFT JOIN transactions t ON t.user_id = w.user_id
            AND (t.wallet_id = w.id OR t.to_wallet_id = w.id)
            WHERE w.user_id = @UserId
            AND w.archived_at IS NULL
        """;

        long totalWalletBalance = await conn.ExecuteScalarAsync<long>(balanceSql, new {UserId});

        return Ok(new
        {
            periode = new
            {
                from = from.ToString("yyyy-MM-dd"),
                to = to.ToString("yyyy-MM-dd")
            },
            income  = totalIncome,
            expense = totalExpense,
            net     = netFlow,
            totalBalance = totalWalletBalance
        });
    }
}