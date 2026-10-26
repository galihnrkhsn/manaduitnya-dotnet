using Dapper;
using Npgsql;

namespace ManaDuitnya.Api.Repositories;

public class RefreshTokenRepository(NpgsqlDataSource  db)
{
    public async Task InsertAsync(long userId, string tokenHash, DateTime expiresAt)
    {
        const string sql = """
            INSERT INTO refresh_tokens
                (user_id, token_hash, expires_at)
            VALUES
                (@userId, @tokenHash, @expiresAt)
        """;

        await using var conn = await db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { userId, tokenHash, expiresAt });
    }

    public async Task<long?> ConsumeAsync(string tokenHash)
    {
        const string sql = """
            UPDATE refresh_tokens
            SET revoked_at = now()
            WHERE token_hash = @tokenHash
            AND revoked_at IS NULL
            AND expires_at > now()
            RETURNING user_id
        """;

        await using var conn = await db.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<long?>(sql, new { tokenHash });
    }

    public async Task RevokeAsync(string tokenHash)
    {
        const string sql = """
            UPDATE refresh_tokens
            SET revoked_at = now()
            WHERE token_hash = @tokenHash AND revoked_at IS NULL
        """;

        await using var conn = await db.OpenConnectionAsync();
        await conn.ExecuteAsync(sql, new { tokenHash });
    }
}