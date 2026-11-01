using GraEvatMiddleware.Application.Common;
using GraEvatMiddleware.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
            ResourceConflictException => (StatusCodes.Status409Conflict, exception.Message),
            IdempotencyConflictException => (StatusCodes.Status409Conflict, exception.Message),
            DomainValidationException => (StatusCodes.Status422UnprocessableEntity, exception.Message),
            ConfigurationMissingException => (StatusCodes.Status503ServiceUnavailable, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled request failure with correlation ID {CorrelationId}.", context.TraceIdentifier);
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = context.Request.Path,
            Extensions = { ["correlationId"] = context.TraceIdentifier }
        });
    }
}
