using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegalSynq.AuditClient.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegalSynq.AuditClient;

/// <summary>
/// IAuditEventClient backed by HttpClient. Registered via AddAuditEventClient().
/// All methods are fire-and-observe — they never throw for transport or HTTP failures.
/// </summary>
public sealed class HttpAuditEventClient : IAuditEventClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private const string SingleEndpoint = "/internal/audit/events";
    private const string BatchEndpoint  = "/internal/audit/events/batch";

    private readonly HttpClient                     _http;
    private readonly AuditClientOptions             _opts;
    private readonly ILogger<HttpAuditEventClient>  _logger;

    public HttpAuditEventClient(
        HttpClient                    http,
        IOptions<AuditClientOptions>  opts,
        ILogger<HttpAuditEventClient> logger)
    {
        _http   = http;
        _opts   = opts.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_opts.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout     = TimeSpan.FromSeconds(_opts.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_opts.ServiceToken))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("x-service-token", _opts.ServiceToken);
    }

    public async Task<IngestResult> IngestAsync(IngestAuditEventRequest request, CancellationToken ct = default)
    {
        using var req = BuildRequest(HttpMethod.Post, SingleEndpoint, request);

        try
        {
            using var res = await _http.SendAsync(req, ct);

            if (res.StatusCode == HttpStatusCode.Created)
            {
                var body    = await res.Content.ReadFromJsonAsync<ApiEnvelope<IngestItemResult>>(JsonOpts, ct);
                var auditId = body?.Data?.AuditId;
                _logger.LogDebug("AuditEvent ingested: AuditId={AuditId} EventType={EventType}", auditId, request.EventType);
                return new IngestResult(true, auditId, null, (int)res.StatusCode);
            }

            if (res.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogDebug("AuditEvent duplicate IdempotencyKey: EventType={EventType}", request.EventType);
                return new IngestResult(true, null, "DuplicateIdempotencyKey", (int)res.StatusCode);
            }

            var errBody = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("AuditEvent ingest rejected: StatusCode={Code} EventType={EventType} Body={Body}",
                (int)res.StatusCode, request.EventType, errBody);
            return new IngestResult(false, null, $"HTTP{(int)res.StatusCode}", (int)res.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuditEvent ingest error: EventType={EventType} ExceptionType={ExType}",
                request.EventType, ex.GetType().Name);
            return new IngestResult(false, null, "ClientError", 0);
        }
    }

    public async Task<BatchIngestResult> IngestBatchAsync(BatchIngestRequest request, CancellationToken ct = default)
    {
        using var req = BuildRequest(HttpMethod.Post, BatchEndpoint, request);

        try
        {
            using var res  = await _http.SendAsync(req, ct);
            var body       = await res.Content.ReadFromJsonAsync<ApiEnvelope<BatchResponseDto>>(JsonOpts, ct);
            var data       = body?.Data;

            if (data is null)
            {
                _logger.LogWarning("AuditEvent batch empty response: StatusCode={Code}", (int)res.StatusCode);
                var empty = new IngestResult[request.Events.Count];
                for (int i = 0; i < empty.Length; i++)
                    empty[i] = new IngestResult(false, null, "EmptyResponse", (int)res.StatusCode);
                return new BatchIngestResult(request.Events.Count, 0, request.Events.Count, empty);
            }

            _logger.LogDebug("AuditEvent batch: Submitted={Sub} Accepted={Acc} Rejected={Rej}",
                data.Submitted, data.Accepted, data.Rejected);

            var results = (data.Results ?? [])
                .Select(r => new IngestResult(r.Accepted, r.AuditId, r.RejectionReason, r.StatusCode))
                .ToArray();

            return new BatchIngestResult(data.Submitted, data.Accepted, data.Rejected, results);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuditEvent batch error: ExceptionType={ExType}", ex.GetType().Name);
            var empty = new IngestResult[request.Events.Count];
            for (int i = 0; i < empty.Length; i++)
                empty[i] = new IngestResult(false, null, "ClientError", 0);
            return new BatchIngestResult(request.Events.Count, 0, request.Events.Count, empty);
        }
    }

    private HttpRequestMessage BuildRequest<T>(HttpMethod method, string path, T body)
    {
        var req = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        return req;
    }

    private sealed class ApiEnvelope<T>      { public T? Data { get; set; } }
    private sealed class IngestItemResult    { public string? AuditId { get; set; } }
    private sealed class BatchResponseDto
    {
        public int Submitted { get; set; }
        public int Accepted  { get; set; }
        public int Rejected  { get; set; }
        public List<BatchItemResult>? Results { get; set; }
    }
    private sealed class BatchItemResult
    {
        public bool    Accepted        { get; set; }
        public string? AuditId         { get; set; }
        public string? RejectionReason { get; set; }
        public int     StatusCode       { get; set; }
    }
}
