namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-005-01: Result of validating a referral view token.
/// Contains the referral ID and the token version embedded in the token.
/// Callers must verify that TokenVersion matches the referral's current TokenVersion
/// to ensure the token has not been revoked.
/// </summary>
public record ViewTokenValidationResult(Guid ReferralId, int TokenVersion);
