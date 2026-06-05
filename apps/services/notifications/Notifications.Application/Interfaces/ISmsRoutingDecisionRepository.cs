using Notifications.Application.DTOs;
using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ISmsRoutingDecisionRepository
{
    Task<SmsRoutingDecision> CreateAsync(SmsRoutingDecision decision, CancellationToken ct = default);
    Task UpdateAttemptIdAsync(Guid decisionId, Guid attemptId, CancellationToken ct = default);
    Task<(IReadOnlyList<SmsRoutingDecision> Items, int Total)> ListAsync(SmsRoutingDecisionQuery query, CancellationToken ct = default);
    Task<SmsRoutingDecisionSummaryDto> GetSummaryAsync(SmsRoutingDecisionQuery query, CancellationToken ct = default);
}
