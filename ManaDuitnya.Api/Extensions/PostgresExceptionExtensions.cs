using Npgsql;

namespace ManaDuitnya.Api.Extensions;

public sealed record DbErrorInfo(int Status, string Code, string Message);
public static class PostgresExceptionExtensions
{
    public static DbErrorInfo ToErrorInfo(
        this PostgresException ex,
        string? entityName = null,
        IReadOnlyDictionary<string, DbErrorInfo>? byConstraint = null)
    {
        // 1. Cocokkan nama constraint persis
        if (ex.ConstraintName is not null &&
            byConstraint is not null &&
            byConstraint.TryGetValue(ex.ConstraintName, out var known))
            return known;

        // 2. Fallback ke SqlState
        return ex.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => new(409, "DUPLICATE_DATA",
                entityName is null ? "Data sudah ada." : $"{entityName} dengan data tersebut sudah ada."),

            PostgresErrorCodes.ForeignKeyViolation => new(409, "INVALID_REFERENCE",
                "Data terkait tidak ditemukan atau masih dipakai."),

            PostgresErrorCodes.NotNullViolation => new(400, "REQUIRED_FIELD_MISSING",
                "Ada data wajib yang belum terisi."),

            PostgresErrorCodes.CheckViolation => new(400, "INVALID_DATA",
                "Data tidak memenuhi aturan."),

            PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure => new(409, "CONFLICT_RETRY",
                "Terjadi konflik, silakan coba lagi."),

            _ => new(500, "DB_ERROR", "Terjadi kendala pada database.")
        };
    }
}