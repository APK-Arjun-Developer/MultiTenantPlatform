using System.Net.Mime;
using Api.Contracts;
using FluentValidation;

namespace Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(ex, "Unhandled exception after response started.");
                throw;
            }

            var (statusCode, message, errors) = MapException(ex);

            _logger.LogError(ex, "Unhandled exception mapped to {StatusCode}.", statusCode);

            var envelope = new ApiEnvelope<object?>
            {
                Data = null,
                Message = message,
                Errors = errors,
                TraceId = context.TraceIdentifier,
            };

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            await context.Response.WriteAsJsonAsync(envelope);
        }
    }

    private static (int statusCode, string message, object? errors) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, ApiEnvelopeFactory.ValidationFailedMessage, ApiEnvelopeFactory.FromFluentValidation(ve)),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized.", new { code = "unauthorized" }),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found.", new { code = "not_found" }),
            InvalidOperationException ioe => MapInvalidOperation(ioe),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", new { code = "unexpected_error" }),
        };
    }

    private static (int statusCode, string message, object? errors) MapInvalidOperation(InvalidOperationException ex)
    {
        var msg = ex.Message ?? string.Empty;

        if (msg.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status404NotFound, msg, new { code = "not_found" });
        }

        if (msg.Contains("already exists", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("already", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("conflict", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status409Conflict, msg, new { code = "conflict" });
        }

        if (msg.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status400BadRequest, msg, new { code = "bad_request" });
        }

        return (StatusCodes.Status400BadRequest, msg, new { code = "bad_request" });
    }

}

