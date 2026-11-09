using System.Diagnostics;
using ManaDuitnya.Api.Dtos;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace ManaDuitnya.Api.Extensions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    ) {
        int statusCode;
        string message;
        string code;

        if (exception is PostgresException pgEx) {
            var info = pgEx.ToErrorInfo();

            logger.Log(
                info.Status >= 500 ? LogLevel.Error : LogLevel.Warning,
                pgEx,
                "DB Error SqlState={SqlState} Constraint={Constraint} Table={Table} Detail={Detail}",
                pgEx.SqlState, pgEx.ConstraintName, pgEx.TableName, pgEx.Detail
            );

            statusCode = info.Status;
            message = info.Message;
            code = info.Code;
        } else if (exception is NpgsqlException npgEx) {
            logger.LogError(npgEx, "Database Unavailable");

            statusCode = 503;
            message = "Database Server sedang bermasalah, coba lagi nanti.";
            code = "DB_UNAVAILABLE";
        } else {
            logger.LogError(exception, "Unhandled Exception");
            
            statusCode = 500;
            message = "Terjadi Kesalahan pada server.";
            code = "INTERNAL_SERVER_ERROR";
        }

        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        var response = ApiResponse.Fail(message, code, new { traceId });

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}
