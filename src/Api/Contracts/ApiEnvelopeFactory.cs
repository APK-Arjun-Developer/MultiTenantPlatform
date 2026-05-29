using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Contracts;

public static class ApiEnvelopeFactory
{
    public const string ValidationFailedMessage = "Validation failed.";

    public static ApiEnvelope<object?> ValidationError(
        object errors,
        string traceId,
        string message = ValidationFailedMessage)
    {
        return new ApiEnvelope<object?>
        {
            Data = null,
            Message = message,
            Errors = errors,
            TraceId = traceId,
        };
    }

    public static object FromModelState(ModelStateDictionary modelState)
    {
        var details = modelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => NormalizeKey(e.Key),
                e => e.Value!.Errors
                    .Select(err => string.IsNullOrEmpty(err.ErrorMessage) ? "Invalid value." : err.ErrorMessage)
                    .Distinct()
                    .ToArray());

        return FromDetails(details);
    }

    public static object FromFluentValidation(ValidationException ex)
    {
        var details = ex.Errors
            .GroupBy(e => string.IsNullOrWhiteSpace(e.PropertyName) ? "_global" : e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return FromDetails(details);
    }

    private static object FromDetails(Dictionary<string, string[]> details) =>
        new
        {
            code = "validation_failed",
            details,
        };

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "_global";
        }

        var lastDot = key.LastIndexOf('.');
        return lastDot >= 0 ? key[(lastDot + 1)..] : key;
    }
}
