using System.Net.Mime;
using Api.Contracts;
using Application.Exceptions;
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

            if (statusCode >= 500)
            {
                _logger.LogError(ex, "Unhandled exception mapped to {StatusCode}.", statusCode);
            }
            else
            {
                _logger.LogWarning(ex, "Client error mapped to {StatusCode}: {Message}", statusCode, message);
            }

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

    private static (int statusCode, string message, object? errors) MapException(Exception ex) =>
        ex switch
        {
            ValidationException ve =>
                (StatusCodes.Status400BadRequest,
                 ApiEnvelopeFactory.ValidationFailedMessage,
                 ApiEnvelopeFactory.FromFluentValidation(ve)),

            NotFoundException =>
                (StatusCodes.Status404NotFound, ex.Message, new { code = "not_found" }),

            ConflictException =>
                (StatusCodes.Status409Conflict, ex.Message, new { code = "conflict" }),

            ForbiddenException =>
                (StatusCodes.Status403Forbidden, ex.Message, new { code = "forbidden" }),

            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized.", new { code = "unauthorized" }),

            KeyNotFoundException =>
                (StatusCodes.Status404NotFound, "Not found.", new { code = "not_found" }),

            InvalidOperationException =>
                (StatusCodes.Status400BadRequest, ex.Message, new { code = "bad_request" }),

            _ =>
                (StatusCodes.Status500InternalServerError,
                 "An unexpected error occurred.",
                 new { code = "unexpected_error" }),
        };
}
