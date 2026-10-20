using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
namespace FlowLab.Web;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            logger.LogDebug("Request cancelled, trace {TraceId}", context.TraceIdentifier);
            return true;
        }
        var (status, detail) = exception switch
        {
            ArgumentException => (400, exception.Message),
            BadHttpRequestException => (400, "Некорректный формат запроса."),
            KeyNotFoundException => (404, exception.Message),
            InvalidOperationException => (409, exception.Message),
            NpgsqlException => (503, "PostgreSQL недоступен. Проверьте подключение к базе данных."),
            _ => (500, "Внутренняя ошибка. Обратитесь к серверному журналу по traceId.")
        };
        if (status >= 500)
        {
            logger.LogError(exception, "API error {StatusCode}, trace {TraceId}", status, context.TraceIdentifier);
        }
        else
        {
            logger.LogWarning("API request rejected {StatusCode}, {ExceptionType}, trace {TraceId}", status, exception.GetType().Name, context.TraceIdentifier);
        }

        await Results.Problem(statusCode: status, title: "Не удалось выполнить действие", detail: detail, extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        return true;
    }
}
