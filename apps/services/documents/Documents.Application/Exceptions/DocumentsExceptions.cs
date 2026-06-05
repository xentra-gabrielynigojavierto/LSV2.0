namespace Documents.Application.Exceptions;

public abstract class DocumentsException : Exception
{
    public abstract int    StatusCode { get; }
    public abstract string ErrorCode  { get; }

    protected DocumentsException(string message) : base(message) { }
}

public sealed class NotFoundException : DocumentsException
{
    public override int    StatusCode => 404;
    public override string ErrorCode  => "NOT_FOUND";

    public NotFoundException(string resource, object id)
        : base($"{resource} not found: {id}") { }
}

public sealed class ForbiddenException : DocumentsException
{
    public override int    StatusCode => 403;
    public override string ErrorCode  => "ACCESS_DENIED";

    public ForbiddenException(string message) : base(message) { }
}

public sealed class ScanBlockedException : DocumentsException
{
    public override int    StatusCode => 403;
    public override string ErrorCode  => "SCAN_BLOCKED";

    public ScanBlockedException(string message) : base(message) { }
}

public sealed class InfectedFileException : DocumentsException
{
    public override int    StatusCode => 422;
    public override string ErrorCode  => "INFECTED_FILE";

    public InfectedFileException(string message) : base(message) { }
}

public sealed class UnsupportedFileTypeException : DocumentsException
{
    public override int    StatusCode => 422;
    public override string ErrorCode  => "UNSUPPORTED_FILE_TYPE";

    public UnsupportedFileTypeException(string message) : base(message) { }
}

public sealed class TenantIsolationException : DocumentsException
{
    public override int    StatusCode => 403;
    public override string ErrorCode  => "TENANT_ISOLATION_VIOLATION";

    public TenantIsolationException() : base("Tenant isolation violation") { }
}

public sealed class TokenExpiredException : DocumentsException
{
    public override int    StatusCode => 401;
    public override string ErrorCode  => "TOKEN_EXPIRED";

    public TokenExpiredException(string message) : base(message) { }
}

public sealed class TokenInvalidException : DocumentsException
{
    public override int    StatusCode => 401;
    public override string ErrorCode  => "TOKEN_INVALID";

    public TokenInvalidException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the scan job queue is saturated and cannot accept new jobs.
/// Maps to HTTP 503 — clients should back off and retry the upload.
/// </summary>
public sealed class QueueSaturationException : DocumentsException
{
    public override int    StatusCode => 503;
    public override string ErrorCode  => "QUEUE_SATURATED";

    public QueueSaturationException()
        : base("Scan queue is saturated — upload rejected. Retry after a short delay.") { }
}

public sealed class ValidationException : DocumentsException
{
    public override int    StatusCode => 400;
    public override string ErrorCode  => "VALIDATION_ERROR";

    public Dictionary<string, string[]> Details { get; }

    public ValidationException(Dictionary<string, string[]> details)
        : base("Request validation failed")
    {
        Details = details;
    }
}

/// <summary>
/// Thrown when an uploaded file exceeds the configured <c>Documents:MaxUploadSizeMb</c> limit.
/// Maps to HTTP 413 — client should reduce file size.
/// </summary>
public sealed class FileTooLargeException : DocumentsException
{
    public override int    StatusCode => 413;
    public override string ErrorCode  => "FILE_TOO_LARGE";

    public long FileSizeBytes { get; }
    public int  LimitMb       { get; }

    public FileTooLargeException(long fileSizeBytes, int limitMb)
        : base($"File size {fileSizeBytes:N0} bytes ({fileSizeBytes / 1_048_576.0:F1} MB) exceeds the maximum upload limit of {limitMb} MB")
    {
        FileSizeBytes = fileSizeBytes;
        LimitMb       = limitMb;
    }
}

/// <summary>
/// Thrown when a file exceeds the configured <c>Documents:MaxScannableFileSizeMb</c> limit
/// and therefore cannot be safely submitted for virus scanning.
/// Maps to HTTP 422 — the file cannot be processed at its current size.
/// </summary>
public sealed class FileSizeExceedsScanLimitException : DocumentsException
{
    public override int    StatusCode => 422;
    public override string ErrorCode  => "FILE_EXCEEDS_SCAN_LIMIT";

    public long FileSizeBytes { get; }
    public int  LimitMb       { get; }

    public FileSizeExceedsScanLimitException(long fileSizeBytes, int limitMb)
        : base($"File size {fileSizeBytes:N0} bytes ({fileSizeBytes / 1_048_576.0:F1} MB) exceeds the maximum scannable limit of {limitMb} MB")
    {
        FileSizeBytes = fileSizeBytes;
        LimitMb       = limitMb;
    }
}
