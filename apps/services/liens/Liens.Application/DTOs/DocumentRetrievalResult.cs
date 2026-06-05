namespace Liens.Application.DTOs;

public sealed class DocumentRetrievalResult : IAsyncDisposable, IDisposable
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public long? ContentLength { get; init; }

    public IDisposable? ResponseOwner { get; init; }

    public void Dispose()
    {
        Content.Dispose();
        ResponseOwner?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
        ResponseOwner?.Dispose();
    }
}
