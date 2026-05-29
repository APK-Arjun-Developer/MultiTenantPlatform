namespace Api.Contracts;

public sealed class ApiEnvelope<T>
{
    public T? Data { get; init; }

    public required string Message { get; init; }

    public object? Errors { get; init; }

    public string? TraceId { get; init; }
}

