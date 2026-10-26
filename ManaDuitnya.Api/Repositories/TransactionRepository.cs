using Dapper;
using ManaDuitnya.Api.Entities;
using Npgsql;

namespace ManaDuitnya.Api.Repositories;

public class TransactionRepository(NpgsqlDataSource db)
{
    public async Task<IEnumerable<Transaction>> ListAsync(long userId, DateTime from, DateTime to)
    {
        const string sql = """
            SELECT uuid, user_id, wallet_id, to_wallet_id, category_id,
                type, amount, note, transaction_date
            FROM transactions
            WHERE user_id = @userId
            AND transaction_date >= @from
            AND transaction_date < @to
            ORDER BY transaction_date DESC
        """;

        await using var conn = await db.OpenConnectionAsync();
        return await conn.QueryAsync<Transaction>(sql, new { userId, from, to });
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