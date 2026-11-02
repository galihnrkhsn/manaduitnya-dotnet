using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
namespace ManaDuitnya.Api.Extensions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    ) {
        ProblemDetails pd;
        int StatusCode;

        if (exception is PostgresException pgEx) {
            var info = pgEx.ToErrorInfo();

            logger.Log(
                info.Status >= 500 ? LogLevel.Error : LogLevel.Warning,
                pgEx,
                "DB Error SqlState={SqlSatate} Constraint={Constraint} Table={Table} Detail={Detail}",
                pgEx.SqlState, pgEx.ConstraintName, pgEx.TableName, pgEx.Detail
            );

            StatusCode = info.Status;
            pd = new ProblemDetails {Status = info.Status, Title = info.Message};
            pd.Extensions["code"] = info.Code;
        } else if (exception is NpgsqlException npgEx) {
            logger.LogError(npgEx, "Database Unavailable");

            StatusCode = 503;
            pd = new ProblemDetails {Status = 503, Title = "Database Server sedang bermasalah, coba lagi nanti."};
            pd.Extensions["code"] = "DB_UNAVAILABLE";
        } else {
            logger.LogError(exception, "Unhandled Exception");
            
            StatusCode = 500;
            pd = new ProblemDetails {Status = 500, Title = "Terjadi Kesalahan."};
            pd.Extensions["code"] = "INTERNAL_SERVER_ERROR";
        }

        pd.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCode;
        await httpContext.Response.WriteAsJsonAsync(pd, cancellationToken);

        return true;
    }
}