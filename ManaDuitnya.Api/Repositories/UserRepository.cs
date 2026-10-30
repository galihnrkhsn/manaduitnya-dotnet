using Dapper;
using ManaDuitnya.Api.Entities;
using Npgsql;

namespace ManaDuitnya.Api.Repositories;

public class UserRepository(NpgsqlDataSource db)
{
    private const string SelectUser = "SELECT id, name, email, password as PasswordHash FROM users";

    public async Task<User?> GetByEmail(string email)
    {
        await using var conn = await db.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"{SelectUser} WHERE email = @email", new { email }
        );
    }

    public async Task<User?> GetById(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"{SelectUser} WHERE id = @id", new {id}
        );
    }

    public async Task<User?> Create(string name, string email, string passwordHash)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var userId = await conn.ExecuteScalarAsync<long>("""
                INSERT INTO users (name, email, password, created_at, updated_at)
                VALUES (@name, @email, @passwordHash, now(), now())
                RETURNING id
            """,
            new { name, email, passwordHash }, tx
        );

        await conn.ExecuteAsync(
            """
                INSERT INTO wallets 
                    (user_id, name, initial_balance, created_at)
                VALUES
                    (@userId, 'tunai', 0, now())
            """,
            new { userId }, tx
        );

        var categories = DefaultCategories.Select(c => new {userId, c.Name, c.Type, c.Color});

        await conn.ExecuteAsync(
            """
                INSERT INTO categories
                    (user_id, name, type, color, created_at)
                VALUES
                    (@userId, @name, @type, @color, now())
            """,
            categories, tx
        );

        await tx.CommitAsync();

        return new User { Id = userId, Name = name, Email = email, PasswordHash = passwordHash };
    }

    private static readonly (string Name, short Type, string Color)[] DefaultCategories = 
    {
        ("Makan & Minum", 2, "#B45309"),
        ("Transportasi", 2, "#0369A1"),
        ("Tagihan", 2, "#475569"),
        ("Belanja", 2, "#BE185D"),
        ("Hiburan", 2, "#65A30D"),
        ("Kesehatan", 2, "#15803D"),
        ("Biaya Admin", 2, "#64748B"),
        ("Lainnya", 2, "#94A3B8"),
        ("Gaji", 1, "'#0D9488"),
        ("Lainnya", 1, "'#14B8A6")
    };
}
