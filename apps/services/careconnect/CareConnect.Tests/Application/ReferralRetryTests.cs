// LSCC-005-02: Operational Automation & Email Reliability — Retry System Tests
// Covers: retry eligibility, delay schedule, derived-state derivation,
//         notification domain methods (MarkFailed with retry scheduling, ClearRetrySchedule),
//         retry/resend distinction, MaxAttempts boundary, edge cases.
using CareConnect.Application.Services;
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-005-02 — Retry system tests.
///
///   ReferralRetryPolicy — eligibility:
///     - Failed + eligible type + Initial source + under MaxAttempts → eligible
///     - Sent notification → not eligible
///     - ManualResend source → not eligible (manual resends have their own lifecycle)
///     - AttemptCount == MaxAttempts → not eligible (exhausted)
///     - AttemptCount > MaxAttempts → not eligible
///     - ReferralEmailResent type → not eligible
///
///   ReferralRetryPolicy — exhaustion:
///     - AttemptCount &lt; MaxAttempts → not exhausted
///     - AttemptCount == MaxAttempts → exhausted
///
///   ReferralRetryPolicy — GetNextRetryAfter delay schedule:
///     - After 1 failure → ~5 min from now
///     - After 2 failures → ~30 min from now
///     - After MaxAttempts failures → null (no more retries)
///     - Edge: attemptCount=0 → null (invalid input)
///
///   ReferralRetryPolicy — GetDerivedStatus:
///     - Sent → "Sent"
///     - Pending → "Pending"
///     - Failed + under max + NextRetryAfterUtc set → "Retrying"
///     - Failed + under max + NextRetryAfterUtc null → "Failed"
///     - Failed + AttemptCount == MaxAttempts → "RetryExhausted"
///     - Failed + AttemptCount > MaxAttempts → "RetryExhausted"
///
///   CareConnectNotification domain:
///     - MarkFailed with nextRetryAfterUtc persists NextRetryAfterUtc
///     - MarkFailed without nextRetryAfterUtc leaves NextRetryAfterUtc null
///     - MarkSent() clears NextRetryAfterUtc
///     - ClearRetrySchedule() sets NextRetryAfterUtc to null
///     - ClearRetrySchedule() does NOT change Status or AttemptCount
///
///   Retry/Resend distinction:
///     - ManualResend source is ineligible for auto-retry
///     - TriggerSource=Initial is eligible for auto-retry
///     - TriggerSource=AutoRetry is eligible for further retries while under MaxAttempts
///
///   NotificationSource constants:
///     - Defined as expected string values
/// </summary>
public class ReferralRetryTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static CareConnectNotification MakeNotification(
        string  type          = NotificationType.ReferralCreated,
        string  source        = NotificationSource.Initial,
        string  status        = NotificationStatus.Failed,
        int     attemptCount  = 1,
        bool    hasRetryTime  = false)
    {
        var n = CareConnectNotification.Create(
            tenantId:          Guid.NewGuid(),
            notificationType:  type,
            relatedEntityType: "Referral",
            relatedEntityId:   Guid.NewGuid(),
            recipientType:     NotificationRecipientType.Provider,
            recipientAddress:  "provider@example.com",
            subject:           "Test",
            message:           "body",
            scheduledForUtc:   null,
            createdByUserId:   null,
            triggerSource:     source);

        // Force state via domain methods
        for (int i = 0; i < attemptCount; i++)
        {
            if (status == NotificationStatus.Sent && i == attemptCount - 1)
                n.MarkSent();
            else
                n.MarkFailed("SMTP error", hasRetryTime ? DateTime.UtcNow.AddMinutes(5) : null);
        }
        return n;
    }

    // ── Policy: eligibility ────────────────────────────────────────────────────

    [Fact]
    public void IsEligibleForRetry_FailedInitialUnderMax_ReturnsTrue()
    {
        var n = MakeNotification(
            type:        NotificationType.ReferralCreated,
            source:      NotificationSource.Initial,
            status:      NotificationStatus.Failed,
            attemptCount: 1);

        Assert.True(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    [Fact]
    public void IsEligibleForRetry_SentNotification_ReturnsFalse()
    {
        var n = MakeNotification(
            status:      NotificationStatus.Sent,
            attemptCount: 1);

        Assert.False(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    [Fact]
    public void IsEligibleForRetry_ManualResendSource_ReturnsFalse()
    {
        var n = MakeNotification(
            type:        NotificationType.ReferralEmailResent,
            source:      NotificationSource.ManualResend,
            attemptCount: 1);

        Assert.False(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    [Fact]
    public void IsEligibleForRetry_AttemptCountEqualsMax_ReturnsFalse()
    {
        var n = MakeNotification(attemptCount: ReferralRetryPolicy.MaxAttempts);

        Assert.False(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    [Fact]
    public void IsEligibleForRetry_AttemptCountExceedsMax_ReturnsFalse()
    {
        // Simulate a notification that somehow accumulated more than MaxAttempts
        var n = MakeNotification(attemptCount: ReferralRetryPolicy.MaxAttempts + 1);

        Assert.False(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    [Fact]
    public void IsEligibleForRetry_ReferralEmailResentType_ReturnsFalse()
    {
        // Manual resend records should never be auto-retried
        var n = MakeNotification(
            type:   NotificationType.ReferralEmailResent,
            source: NotificationSource.ManualResend);

        Assert.False(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    [Theory]
    [InlineData(NotificationType.ReferralAcceptedProvider)]
    [InlineData(NotificationType.ReferralAcceptedReferrer)]
    [InlineData(NotificationType.ReferralEmailAutoRetry)]
    public void IsEligibleForRetry_AllEligibleTypes_ReturnTrue(string type)
    {
        var n = MakeNotification(type: type, attemptCount: 1);
        Assert.True(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    // ── Policy: exhaustion ─────────────────────────────────────────────────────

    [Fact]
    public void IsExhausted_UnderMaxAttempts_ReturnsFalse()
    {
        var n = MakeNotification(attemptCount: ReferralRetryPolicy.MaxAttempts - 1);
        Assert.False(ReferralRetryPolicy.IsExhausted(n));
    }

    [Fact]
    public void IsExhausted_AtMaxAttempts_ReturnsTrue()
    {
        var n = MakeNotification(attemptCount: ReferralRetryPolicy.MaxAttempts);
        Assert.True(ReferralRetryPolicy.IsExhausted(n));
    }

    [Fact]
    public void IsExhausted_SentNotification_ReturnsFalse()
    {
        // A sent notification is not exhausted — it succeeded
        var n = MakeNotification(status: NotificationStatus.Sent, attemptCount: 2);
        Assert.False(ReferralRetryPolicy.IsExhausted(n));
    }

    // ── Policy: GetNextRetryAfter delay schedule ───────────────────────────────

    [Fact]
    public void GetNextRetryAfter_AfterFirstFailure_IsApproximately5Minutes()
    {
        var before = DateTime.UtcNow;
        var result = ReferralRetryPolicy.GetNextRetryAfter(1);
        var after  = DateTime.UtcNow;

        Assert.NotNull(result);
        Assert.InRange(result!.Value,
            before.AddMinutes(4).AddSeconds(59),
            after.AddMinutes(5).AddSeconds(1));
    }

    [Fact]
    public void GetNextRetryAfter_AfterSecondFailure_IsApproximately30Minutes()
    {
        var before = DateTime.UtcNow;
        var result = ReferralRetryPolicy.GetNextRetryAfter(2);
        var after  = DateTime.UtcNow;

        Assert.NotNull(result);
        Assert.InRange(result!.Value,
            before.AddMinutes(29).AddSeconds(59),
            after.AddMinutes(30).AddSeconds(1));
    }

    [Fact]
    public void GetNextRetryAfter_AtMaxAttempts_ReturnsNull()
    {
        var result = ReferralRetryPolicy.GetNextRetryAfter(ReferralRetryPolicy.MaxAttempts);
        Assert.Null(result);
    }

    [Fact]
    public void GetNextRetryAfter_ZeroAttempts_ReturnsNull()
    {
        // 0 is an invalid input — treated as no retry
        var result = ReferralRetryPolicy.GetNextRetryAfter(0);
        Assert.Null(result);
    }

    // ── Policy: GetDerivedStatus ───────────────────────────────────────────────

    [Fact]
    public void GetDerivedStatus_SentNotification_ReturnsSent()
    {
        var n = MakeNotification(status: NotificationStatus.Sent, attemptCount: 1);
        Assert.Equal("Sent", ReferralRetryPolicy.GetDerivedStatus(n));
    }

    [Fact]
    public void GetDerivedStatus_PendingNotification_ReturnsPending()
    {
        var n = CareConnectNotification.Create(
            tenantId: Guid.NewGuid(), notificationType: NotificationType.ReferralCreated,
            relatedEntityType: "Referral", relatedEntityId: Guid.NewGuid(),
            recipientType: NotificationRecipientType.Provider, recipientAddress: "p@x.com",
            subject: "s", message: "m", scheduledForUtc: null, createdByUserId: null,
            triggerSource: NotificationSource.Initial);
        Assert.Equal("Pending", ReferralRetryPolicy.GetDerivedStatus(n));
    }

    [Fact]
    public void GetDerivedStatus_FailedWithRetryScheduled_ReturnsRetrying()
    {
        var n = MakeNotification(attemptCount: 1, hasRetryTime: true);
        Assert.Equal("Retrying", ReferralRetryPolicy.GetDerivedStatus(n));
    }

    [Fact]
    public void GetDerivedStatus_FailedWithoutRetryScheduled_ReturnsFailed()
    {
        var n = MakeNotification(attemptCount: 1, hasRetryTime: false);
        Assert.Equal("Failed", ReferralRetryPolicy.GetDerivedStatus(n));
    }

    [Fact]
    public void GetDerivedStatus_FailedAtMaxAttempts_ReturnsRetryExhausted()
    {
        var n = MakeNotification(attemptCount: ReferralRetryPolicy.MaxAttempts);
        Assert.Equal("RetryExhausted", ReferralRetryPolicy.GetDerivedStatus(n));
    }

    [Fact]
    public void GetDerivedStatus_FailedExceedsMaxAttempts_ReturnsRetryExhausted()
    {
        var n = MakeNotification(attemptCount: ReferralRetryPolicy.MaxAttempts + 1);
        Assert.Equal("RetryExhausted", ReferralRetryPolicy.GetDerivedStatus(n));
    }

    // ── Domain: CareConnectNotification — retry fields ─────────────────────────

    [Fact]
    public void MarkFailed_WithNextRetryAfterUtc_PersistsSchedule()
    {
        var n        = MakeNotification(attemptCount: 0); // create without any MarkFailed calls
        var retry    = DateTime.UtcNow.AddMinutes(5);
        // Already has one MarkFailed from MakeNotification with attemptCount=0
        // so let's create fresh:
        var fresh = CareConnectNotification.Create(
            tenantId: Guid.NewGuid(), notificationType: NotificationType.ReferralCreated,
            relatedEntityType: "Referral", relatedEntityId: Guid.NewGuid(),
            recipientType: NotificationRecipientType.Provider, recipientAddress: "p@x.com",
            subject: "s", message: "m", scheduledForUtc: null, createdByUserId: null,
            triggerSource: NotificationSource.Initial);

        fresh.MarkFailed("SMTP error", retry);

        Assert.Equal(NotificationStatus.Failed, fresh.Status);
        Assert.NotNull(fresh.NextRetryAfterUtc);
        Assert.True(fresh.NextRetryAfterUtc!.Value >= DateTime.UtcNow.AddMinutes(4));
    }

    [Fact]
    public void MarkFailed_WithoutNextRetryAfterUtc_LeavesScheduleNull()
    {
        var n = CareConnectNotification.Create(
            tenantId: Guid.NewGuid(), notificationType: NotificationType.ReferralCreated,
            relatedEntityType: "Referral", relatedEntityId: Guid.NewGuid(),
            recipientType: NotificationRecipientType.Provider, recipientAddress: "p@x.com",
            subject: "s", message: "m", scheduledForUtc: null, createdByUserId: null,
            triggerSource: NotificationSource.Initial);

        n.MarkFailed("SMTP error");

        Assert.Null(n.NextRetryAfterUtc);
    }

    [Fact]
    public void MarkSent_ClearsNextRetryAfterUtc()
    {
        var n = CareConnectNotification.Create(
            tenantId: Guid.NewGuid(), notificationType: NotificationType.ReferralCreated,
            relatedEntityType: "Referral", relatedEntityId: Guid.NewGuid(),
            recipientType: NotificationRecipientType.Provider, recipientAddress: "p@x.com",
            subject: "s", message: "m", scheduledForUtc: null, createdByUserId: null,
            triggerSource: NotificationSource.Initial);

        n.MarkFailed("SMTP error", DateTime.UtcNow.AddMinutes(5));
        Assert.NotNull(n.NextRetryAfterUtc);

        n.MarkSent();
        Assert.Null(n.NextRetryAfterUtc);
        Assert.Equal(NotificationStatus.Sent, n.Status);
    }

    [Fact]
    public void ClearRetrySchedule_SetsNextRetryAfterUtcToNull()
    {
        var n = CareConnectNotification.Create(
            tenantId: Guid.NewGuid(), notificationType: NotificationType.ReferralCreated,
            relatedEntityType: "Referral", relatedEntityId: Guid.NewGuid(),
            recipientType: NotificationRecipientType.Provider, recipientAddress: "p@x.com",
            subject: "s", message: "m", scheduledForUtc: null, createdByUserId: null,
            triggerSource: NotificationSource.Initial);

        n.MarkFailed("SMTP error", DateTime.UtcNow.AddMinutes(5));
        n.ClearRetrySchedule();

        Assert.Null(n.NextRetryAfterUtc);
    }

    [Fact]
    public void ClearRetrySchedule_DoesNotChangeStatusOrAttemptCount()
    {
        var n = CareConnectNotification.Create(
            tenantId: Guid.NewGuid(), notificationType: NotificationType.ReferralCreated,
            relatedEntityType: "Referral", relatedEntityId: Guid.NewGuid(),
            recipientType: NotificationRecipientType.Provider, recipientAddress: "p@x.com",
            subject: "s", message: "m", scheduledForUtc: null, createdByUserId: null,
            triggerSource: NotificationSource.Initial);

        n.MarkFailed("SMTP error", DateTime.UtcNow.AddMinutes(5));
        n.MarkFailed("SMTP error again", DateTime.UtcNow.AddMinutes(30));
        var countBefore  = n.AttemptCount;
        var statusBefore = n.Status;

        n.ClearRetrySchedule();

        Assert.Equal(countBefore,  n.AttemptCount);
        Assert.Equal(statusBefore, n.Status);
    }

    // ── Retry/Resend distinction ───────────────────────────────────────────────

    [Fact]
    public void AutoRetrySource_IsEligibleForRetry()
    {
        var n = MakeNotification(
            type:   NotificationType.ReferralCreated,
            source: NotificationSource.AutoRetry,
            attemptCount: 2);

        // AttemptCount=2, MaxAttempts=3, so still eligible
        Assert.True(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    [Fact]
    public void ManualResendSource_IsNeverEligibleForAutoRetry()
    {
        // Manual resend records must never be picked up by the auto-retry worker
        var n = MakeNotification(
            type:         NotificationType.ReferralEmailResent,
            source:       NotificationSource.ManualResend,
            attemptCount: 1);

        Assert.False(ReferralRetryPolicy.IsEligibleForRetry(n));
    }

    [Fact]
    public void TriggerSource_DefaultsToInitial()
    {
        var n = CareConnectNotification.Create(
            tenantId: Guid.NewGuid(), notificationType: NotificationType.ReferralCreated,
            relatedEntityType: "Referral", relatedEntityId: Guid.NewGuid(),
            recipientType: NotificationRecipientType.Provider, recipientAddress: "p@x.com",
            subject: "s", message: "m", scheduledForUtc: null, createdByUserId: null,
            triggerSource: NotificationSource.Initial);

        Assert.Equal(NotificationSource.Initial, n.TriggerSource);
    }

    // ── NotificationSource constants ───────────────────────────────────────────

    [Fact]
    public void NotificationSource_InitialConstant_IsCorrect()
        => Assert.Equal("Initial", NotificationSource.Initial);

    [Fact]
    public void NotificationSource_AutoRetryConstant_IsCorrect()
        => Assert.Equal("AutoRetry", NotificationSource.AutoRetry);

    [Fact]
    public void NotificationSource_ManualResendConstant_IsCorrect()
        => Assert.Equal("ManualResend", NotificationSource.ManualResend);

    // ── MaxAttempts constant ───────────────────────────────────────────────────

    [Fact]
    public void MaxAttempts_IsThree()
        => Assert.Equal(3, ReferralRetryPolicy.MaxAttempts);

    // ── AttemptCount integrity across multiple failures ────────────────────────

    [Fact]
    public void AttemptCount_IncrementsByOnePerMarkFailed()
    {
        var n = CareConnectNotification.Create(
            tenantId: Guid.NewGuid(), notificationType: NotificationType.ReferralCreated,
            relatedEntityType: "Referral", relatedEntityId: Guid.NewGuid(),
            recipientType: NotificationRecipientType.Provider, recipientAddress: "p@x.com",
            subject: "s", message: "m", scheduledForUtc: null, createdByUserId: null,
            triggerSource: NotificationSource.Initial);

        Assert.Equal(0, n.AttemptCount);
        n.MarkFailed("err1");
        Assert.Equal(1, n.AttemptCount);
        n.MarkFailed("err2", DateTime.UtcNow.AddMinutes(30));
        Assert.Equal(2, n.AttemptCount);
        n.MarkFailed("err3");
        Assert.Equal(3, n.AttemptCount);

        // At MaxAttempts, IsExhausted should be true
        Assert.True(ReferralRetryPolicy.IsExhausted(n));
        Assert.False(ReferralRetryPolicy.IsEligibleForRetry(n));
    }
}
