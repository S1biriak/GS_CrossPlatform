using LibraryApi.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Infrastructure;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var domain = exception as DomainException;
        var sqlite = exception as SqliteException ?? exception.InnerException as SqliteException;
        var status = domain?.StatusCode ?? (sqlite?.SqliteErrorCode is 19 or 5 or 6 || exception is DbUpdateConcurrencyException ? 409 : 500);
        var detail = domain?.Message ?? (status == 409
            ? "Нарушена уникальность, связь данных или данные одновременно изменяются. Проверьте состояние и повторите запрос."
            : "Внутренняя ошибка сервера. Для диагностики используйте traceId.");
        if (status == 500) logger.LogError(exception, "Unhandled request error {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status switch { 400 => "Некорректные данные", 401 => "Требуется вход", 404 => "Ресурс не найден", 409 => "Конфликт состояния", _ => "Ошибка сервера" },
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        return true;
    }
}
