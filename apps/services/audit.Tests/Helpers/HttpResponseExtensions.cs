using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlatformAuditEventService.DTOs;

namespace PlatformAuditEventService.Tests.Helpers;

/// <summary>
/// Extension helpers for deserializing <see cref="ApiResponse{T}"/> envelopes
/// returned by integration test HTTP calls.
/// </summary>
public static class HttpResponseExtensions
{
    /// <summary>
    /// JSON options matching the service's controller serializer:
    ///   - Case-insensitive property matching (handles ASP.NET Core camelCase output)
    ///   - JsonStringEnumConverter so enum fields deserialize correctly
    /// </summary>
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters                  = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Reads the response body and deserializes it as <see cref="ApiResponse{T}"/>.
    /// Throws <see cref="InvalidOperationException"/> when deserialization fails.
    /// </summary>
    public static async Task<ApiResponse<T>> ReadApiResponseAsync<T>(
        this HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOpts)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize ApiResponse<{typeof(T).Name}> from JSON: {json}");
        return result;
    }

    /// <summary>Reads the response body as a raw <see cref="JsonDocument"/>.</summary>
    public static async Task<JsonDocument> ReadJsonAsync(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Posts <paramref name="value"/> as JSON using the service's serializer options
    /// so that enum values are sent as strings (matching the controller's expectations).
    /// </summary>
    public static Task<HttpResponseMessage> PostServiceJsonAsync<T>(
        this HttpClient client, string requestUri, T value) =>
        client.PostAsJsonAsync(requestUri, value, JsonOpts);
}
