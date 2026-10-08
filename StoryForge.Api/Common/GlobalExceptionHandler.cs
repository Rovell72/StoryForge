using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace StoryForge.Api.Common;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                    new ValidationProblemDetails(errors)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Ошибка валидации"
                    },
                    cancellationToken);
                return true;

            case NotFoundException:
                await Write(httpContext, StatusCodes.Status404NotFound, "Не найдено", exception.Message, cancellationToken);
                return true;

            case ConflictException:
                await Write(httpContext, StatusCodes.Status409Conflict, "Конфликт", exception.Message, cancellationToken);
                return true;

            case ForbiddenException:
                await Write(httpContext, StatusCodes.Status403Forbidden, "Доступ запрещён", exception.Message, cancellationToken);
                return true;

            case DbUpdateConcurrencyException:
                await Write(httpContext, StatusCodes.Status409Conflict, "Конфликт", "Данные были изменены другим запросом, повторите попытку", cancellationToken);
                return true;

            default:
                logger.LogError(exception, "Необработанная ошибка");
                return false;
        }
    }

    private static async Task Write(HttpContext context, int status, string title, string detail, CancellationToken ct)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title, Detail = detail },
            ct);
    }
}
