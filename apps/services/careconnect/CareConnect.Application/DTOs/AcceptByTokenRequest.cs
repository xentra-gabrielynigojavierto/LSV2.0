namespace CareConnect.Application.DTOs;

/// <summary>LSCC-005: Body for the public token-based referral acceptance endpoint.</summary>
public class AcceptByTokenRequest
{
    /// <summary>The HMAC-signed view token generated at referral creation time.</summary>
    public string Token { get; set; } = string.Empty;
}
