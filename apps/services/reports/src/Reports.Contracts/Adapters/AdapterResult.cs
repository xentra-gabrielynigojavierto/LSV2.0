namespace Reports.Contracts.Adapters;

public sealed class AdapterResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsRetryable { get; init; }
    public IDictionary<string, string>? Metadata { get; init; }

    public static AdapterResult<T> Ok(T data, IDictionary<string, string>? metadata = null) => new()
    {
        Success = true,
        Data = data,
        Metadata = metadata,
    };

    public static AdapterResult<T> Fail(string errorCode, string errorMessage, bool isRetryable = false) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        IsRetryable = isRetryable,
    };
}
