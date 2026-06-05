namespace BuildingBlocks.Exceptions;

public class ConflictException : Exception
{
    public string? ErrorCode { get; }

    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}
