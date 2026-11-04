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
    public async Task<IActionResult> Index()
    {
        var today          = DateTime.Today;
        var monthStart     = new DateTime(today.Year, today.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);          // batas atas eksklusif (setara endOfMonth)
        var budgetMonth    = DateOnly.FromDateTime(monthStart);

        await using var conn = await db.OpenConnectionAsync();

        // 1. Wallet + saldo
        const string walletSql = """
            SELECT
                w.id,
                w.name,
                w.initial_balance,
                (
                    w.initial_balance
                    + COALESCE((SELECT SUM(amount) FROM transactions
                                WHERE user_id = w.user_id AND wallet_id = w.id AND type = 1), 0)
                    - COALESCE((SELECT SUM(amount) FROM transactions
                                WHERE user_id = w.user_id AND wallet_id = w.id AND type = 2), 0)
                    - COALESCE((SELECT SUM(amount) FROM transactions
                                WHERE user_id = w.user_id AND wallet_id = w.id AND type = 3), 0)
                    + COALESCE((SELECT SUM(amount) FROM transactions
                                WHERE user_id = w.user_id AND to_wallet_id = w.id AND type = 3), 0)
                )::bigint AS balance
            FROM wallets w
            WHERE w.user_id = @UserId
            AND w.archived_at IS NULL
            ORDER BY w.id
            """;
        var wallets      = (await conn.QueryAsync<WalletDto>(walletSql, new { UserId })).ToList();
        long totalBalance = wallets.Sum(w => w.Balance);

        // 2. Income & expense bulan ini
        const string totalsSql = """
            SELECT
                COALESCE(SUM(amount) FILTER (WHERE type = 1), 0)::bigint AS total_income,
                COALESCE(SUM(amount) FILTER (WHERE type = 2), 0)::bigint AS total_expense
            FROM transactions
            WHERE user_id = @UserId
            AND transaction_date >= @monthStart
            AND transaction_date <  @nextMonthStart
            """;
        var totals = await conn.QueryFirstAsync(totalsSql, new { UserId, monthStart, nextMonthStart });
        long income  = (long)totals.total_income;
        long expense = (long)totals.total_expense;

        // 3. Budget + total terpakai (1 query, tanpa N+1)
        const string budgetSql = """
            SELECT
                b.category_id,
                c.name AS category_name,
                b.amount::bigint AS amount,
                COALESCE(SUM(t.amount), 0)::bigint AS spent
            FROM budgets b
            JOIN categories c ON c.id = b.category_id
            LEFT JOIN transactions t
                ON t.user_id = b.user_id
                AND t.category_id = b.category_id
                AND t.type = 2
                AND t.transaction_date >= @monthStart
                AND t.transaction_date <  @nextMonthStart
            WHERE b.user_id = @UserId
            AND b.month = @budgetMonth
            GROUP BY b.uuid, b.category_id, c.name, b.amount
            """;
        var budgets = await conn.QueryAsync<BudgetRow>(budgetSql,
            new { UserId, monthStart, nextMonthStart, budgetMonth });

        var alerts = budgets
            .Select(b => new
            {
                Budget   = b,
                Progress = b.Amount > 0 ? (double)b.Spent / b.Amount * 100 : 0
            })
            .Where(x => x.Progress >= 80)
            .Select(x => new
            {
                category_name = x.Budget.CategoryName,
                progress      = (int)Math.Min(100, Math.Round(x.Progress, MidpointRounding.AwayFromZero)),
                remaining     = x.Budget.Amount - x.Budget.Spent,
                is_over       = x.Budget.Spent >= x.Budget.Amount
            })
            .ToList();

        // 4. 5 transaksi terbaru
        const string recentSql = """
            SELECT
                t.uuid, t.type, t.amount, t.transaction_date,
                t.wallet_id,   w.name AS wallet_name,
                t.category_id, c.name AS category_name
            FROM transactions t
            LEFT JOIN wallets    w ON w.id = t.wallet_id
            LEFT JOIN categories c ON c.id = t.category_id
            WHERE t.user_id = @UserId
            ORDER BY t.transaction_date DESC
            LIMIT 5
            """;
        var recentTransactions = (await conn.QueryAsync<RecentTransactionDto>(recentSql, new { UserId })).ToList();

        return Ok(new
        {
            quotes             = Quotes,
            wallets,
            totalBalance,
            income,
            expense,
            alerts,
            recentTransactions
        });
    }
    private static readonly string[] Quotes =
    [
        "Mencatat pengeluaran, biar tahu miskinnya karena apa.",
        "Gaya elit, ekonomi sulit. Yuk catat pengeluaranmu!",
        "Kerja keras bagai quda, saldo habis entah ke mana.",
        "Uang gajian cuma numpang lewat. Mari absen ke mana dia pergi.",
        "Sudah siap melihat saldo yang bikin istighfar hari ini?",
        "Dompet teriak, hati menangis. Semangat mencatat!",
        "Info loker yang gajinya nutupin gaya hidup dong.",
        "Catat yang rajin! Walau ujung-ujungnya tetep bokek juga.",
        "Jangan lupa daratan, apalagi lupa sisa saldo.",
        "Uang bukan segalanya, tapi segalanya butuh uang."
    ];

    public class WalletDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public long InitialBalance { get; set; }
        public long Balance { get; set; }
    }

    public class BudgetRow
    {
        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public long Amount { get; set; }
        public long Spent { get; set; }
    }

    public class RecentTransactionDto
    {
        public Guid Uuid { get; set; }
        public int Type { get; set; }
        public long Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public long WalletId { get; set; }
        public string? WalletName { get; set; }
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }
}