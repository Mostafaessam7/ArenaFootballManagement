using Arena.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Arena.Api;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, detail) = exception switch {
            BusinessException business => (business.StatusCode, business.Code, business.Message),
            DbUpdateConcurrencyException => (409, "concurrency_conflict", "Data changed during this operation. Reload and retry."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 547 } } =>
                (409, "constraint_conflict", "This request violates a relationship, uniqueness, or data rule."),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 19 } } =>
                (409, "constraint_conflict", "This request violates a relationship, uniqueness, or data rule."),
            SqlException { Number: 1205 } => (409, "transaction_conflict", "A concurrent transaction conflicted. Retry the complete request."),
            SqliteException { SqliteErrorCode: 5 or 6 } => (409, "transaction_conflict", "Database is busy. Retry the complete request."),
            _ => (500, "internal_error", "An unexpected error occurred. Use the trace ID when reporting the issue.")
        };
        if (status == 500) logger.LogError(exception, "Unhandled error {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail,
            Instance = context.Request.Path };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken: ct);
        return true;
    }
}
