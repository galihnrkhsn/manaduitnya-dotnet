using Dapper;
using ManaDuitnya.Api.Entities;
using Npgsql;

namespace ManaDuitnya.Api.Repositories;

public class UserRepository(NpgsqlDataSource db)
{
    private const string SelectUser = "SELECT id, name, email, password as PasswordHash FROM users";

    public async Task<User?> GetByEmailAsync(string email)
    {
        await using var conn = await db.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"{SelectUser} WHERE email = @email", new { email }
        );
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"{SelectUser} WHERE id = @id", new {id}
        );
    }

    public async Task<User?> CreateAsync(string name, string email, string passwordHash)
    {
        const string sql = """
            INSERT INTO users (name, email, password, created_at, updated_at)
            VALUES (@name, @email, @passwordHash, now(), now())
            RETURNING id
        """;

        await using var conn = await db.OpenConnectionAsync();
        var id = await conn.ExecuteScalarAsync<long>(sql, new { name, email, passwordHash });
        return new User { Id = id, Name = name, Email = email, PasswordHash = passwordHash };
    }
}
