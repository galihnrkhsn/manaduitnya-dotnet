using System.Text;
using Dapper;
using ManaDuitnya.Api.Entities;
using Npgsql;

namespace ManaDuitnya.Api.Repositories;

public class TransactionRepository(NpgsqlDataSource db)
{
    public async Task<IEnumerable<Transaction>> ListAsync(long userId, DateTime from, DateTime to, long? walletId, long? categoryId, TransactionType? type, string? search)
    {
        var sql = new StringBuilder("""
            SELECT uuid, user_id, wallet_id, to_wallet_id, category_id,
                type, amount, note, transaction_date
            FROM transactions
            WHERE user_id = @userId
            AND transaction_date >= @from
            AND transaction_date < @to
            """);

        var p = new DynamicParameters();
        p.Add("userId", userId);
        p.Add("from", from);
        p.Add("to", to);

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
            p.Add("type", (int) type);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql.AppendLine(" AND note ILIKE @search");
            p.Add("search", $"%{search.Trim()}%");
        }

        sql.AppendLine(" ORDER BY transaction_date DESC");

        await using var conn = await db.OpenConnectionAsync();
        return await conn.QueryAsync<Transaction>(sql.ToString(), p);
    }

    public async Task<IEnumerable<Transaction>> GetByUuid(string uuid, long UserId)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = conn.QueryFirstAsync("""
            SELECT user_id, wallet_id, to_wallet_id, category_id,
                type, amount, note, transaction_date, created_at, updated_at
            FROM transactions
            WHERE uuid = @uuid
            AND user_id = @userId
        """, new { uuid, UserId });
        return await row;
    }

    public async Task InsertAsync(Transaction t)
    {
        const string sql = """
            INSERT INTO transactions
                (uuid, user_id, wallet_id, to_wallet_id, category_id, type, amount, note, transaction_date)
            VALUES
                (@Uuid, @UserId, @WalletId, @ToWalletId, @CategoryId, @Type, @Amount, @Note, @TransactionDate)
        """;

        await using var conn = await db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new
        {
            t.Uuid, t.UserId, t.WalletId, t.ToWalletId, t.CategoryId, Type = (short)t.Type, t.Amount, t.Note, t.TransactionDate
        });
    }
}