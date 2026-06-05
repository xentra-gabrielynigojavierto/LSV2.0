namespace Support.Api.Files;

/// <summary>Base type for storage-provider errors. Mapped to HTTP responses
/// at the endpoint layer.</summary>
public class SupportFileStorageException : Exception
{
    public SupportFileStorageException(string message) : base(message) { }
    public SupportFileStorageException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Storage is not configured (Mode=NoOp). Mapped to 503.</summary>
public sealed class SupportFileStorageNotConfiguredException : SupportFileStorageException
{
    public SupportFileStorageNotConfiguredException()
        : base("File upload storage provider is not configured.") { }
}

/// <summary>External storage (Documents Service) failed. Mapped to 502.</summary>
public sealed class SupportFileStorageRemoteException : SupportFileStorageException
{
    public int? UpstreamStatusCode { get; }
    public SupportFileStorageRemoteException(string message, int? upstreamStatusCode = null)
        : base(message)
    {
        UpstreamStatusCode = upstreamStatusCode;
    }
    public SupportFileStorageRemoteException(string message, Exception inner, int? upstreamStatusCode = null)
        : base(message, inner)
    {
        UpstreamStatusCode = upstreamStatusCode;
    }
}
