using Dapper;
using ManaDuitnya.Api.Entities;
using Npgsql;

namespace ManaDuitnya.Api.Repositories;

public class WalletRepository(NpgsqlDataSource db)
{
    private const string SelectWallet = "SELECT id, user_id, name, initial_balance, archived_at, created_at, updated_at FROM wallets"; 
    public async Task<IEnumerable<Wallet>> ListAsync(long userId)
    {
        await using var conn = await db.OpenConnectionAsync();
        return await conn.QueryAsync<Wallet>(
            $"{SelectWallet} WHERE user_id = @userId AND archived_at IS NULL ORDER BY created_at DESC", 
            new { userId }
        );
    }

    public async Task<Wallet> InsertAsync(Wallet w)
    {
        const string sql = """
            INSERT INTO wallets
                (user_id, name, initial_balance, created_at, updated_at)
            VALUES
                (@userId, @name, @initialBalance, now(), now())
        """;

        await using var conn = await db.OpenConnectionAsync();
        w.Id = await conn.ExecuteScalarAsync<long>(sql, w);
        return w;
    }

    public async Task<Wallet?> GetByIdAsync(long id, long userId)
    {
        await using var conn = await db.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Wallet>($"{SelectWallet} WHERE id = @id AND user_id = @userId", new { userId, id });
    }

    public async Task<IEnumerable<Wallet>> GetArchivedAsync()
    {
        await using var conn = await db.OpenConnectionAsync();
        return await conn.QueryAsync<Wallet>($"{SelectWallet} WHERE archived_at IS NOT NULL ORDER BY archived_at DESC");
    }

    public async Task<bool> ArchiveAsync(long id, long userId)
    {
        const string sql = """
            UPDATE wallets
            SET archived_at = now(), updated_at = now()
            WHERE id = @id AND user_id = @userId AND archived_at IS NULL
        """;

        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.ExecuteAsync(sql, new { id, userId });
        return row > 0;
    }
}