namespace Documents.Application.DTOs;

public sealed class IssuedTokenResponse
{
    public string AccessToken      { get; init; } = string.Empty;
    public string RedeemUrl        { get; init; } = string.Empty;
    public int    ExpiresInSeconds { get; init; }
    public string Type             { get; init; } = "view";
}
