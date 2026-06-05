using BuildingBlocks.Authorization;
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

public class ReferralWorkflowRulesTests
{
    // ── Canonical transitions ─────────────────────────────────────────────────
    // LSCC-01-001-01: InProgress replaces Scheduled as the canonical active state.
    // Accepted → Completed is explicitly blocked.
    [Theory]
    [InlineData("New",        "Accepted",   true)]
    [InlineData("New",        "Declined",   true)]
    [InlineData("New",        "Cancelled",  true)]
    [InlineData("New",        "InProgress", false)]
    [InlineData("New",        "Scheduled",  false)]
    [InlineData("Accepted",   "InProgress", true)]
    [InlineData("Accepted",   "Declined",   true)]
    [InlineData("Accepted",   "Cancelled",  true)]
    [InlineData("Accepted",   "Completed",  false)]  // blocked — must go through InProgress
    [InlineData("Accepted",   "New",        false)]
    [InlineData("Accepted",   "Scheduled",  false)]  // Scheduled no longer a valid target
    [InlineData("InProgress", "Completed",  true)]
    [InlineData("InProgress", "Cancelled",  true)]
    [InlineData("InProgress", "Accepted",   false)]
    [InlineData("InProgress", "Declined",   false)]
    [InlineData("Completed",  "Cancelled",  false)]
    [InlineData("Declined",   "Accepted",   false)]
    [InlineData("Cancelled",  "New",        false)]
    public void IsValidTransition_CanonicalStatuses_ReturnsExpected(string from, string to, bool expected)
    {
        Assert.Equal(expected, ReferralWorkflowRules.IsValidTransition(from, to));
    }

    // ── Legacy transitions (old data rows that haven't been migrated yet) ─────
    [Theory]
    [InlineData("Received",  "Accepted",   true)]
    [InlineData("Received",  "InProgress", true)]
    [InlineData("Received",  "Declined",   true)]
    [InlineData("Received",  "Cancelled",  true)]
    [InlineData("Contacted", "Accepted",   true)]
    [InlineData("Contacted", "InProgress", true)]
    [InlineData("Contacted", "Declined",   true)]
    [InlineData("Contacted", "Cancelled",  true)]
    // Scheduled is now a legacy status; can only transition to InProgress or Cancelled
    [InlineData("Scheduled", "InProgress", true)]
    [InlineData("Scheduled", "Cancelled",  true)]
    [InlineData("Scheduled", "Completed",  false)]
    [InlineData("Scheduled", "Accepted",   false)]
    public void IsValidTransition_LegacyStatuses_AllowsTransitionToCanonical(string from, string to, bool expected)
    {
        Assert.Equal(expected, ReferralWorkflowRules.IsValidTransition(from, to));
    }

    // ── Terminal states ───────────────────────────────────────────────────────
    [Theory]
    [InlineData("Completed",  true)]
    [InlineData("Declined",   true)]
    [InlineData("Cancelled",  true)]
    [InlineData("New",        false)]
    [InlineData("Accepted",   false)]
    [InlineData("InProgress", false)]  // InProgress is active, not terminal
    public void IsTerminal_ReturnsExpected(string status, bool expected)
    {
        Assert.Equal(expected, ReferralWorkflowRules.IsTerminal(status));
    }

    [Theory]
    [InlineData("Accepted",   PermissionCodes.ReferralAccept)]
    [InlineData("Declined",   PermissionCodes.ReferralDecline)]
    [InlineData("Cancelled",  PermissionCodes.ReferralCancel)]
    [InlineData("InProgress", PermissionCodes.ReferralUpdateStatus)]
    [InlineData("Completed",  PermissionCodes.ReferralUpdateStatus)]
    public void RequiredPermissionFor_ReturnsExpected(string toStatus, string expectedPerm)
    {
        Assert.Equal(expectedPerm, ReferralWorkflowRules.RequiredPermissionFor(toStatus));
    }

    // ── ValidStatuses.All contains canonical values ───────────────────────────
    [Fact]
    public void ValidStatuses_All_ContainsCanonicalValues()
    {
        var all = Referral.ValidStatuses.All;
        Assert.Contains("New",        all);
        Assert.Contains("Accepted",   all);
        Assert.Contains("InProgress", all);
        Assert.Contains("Completed",  all);
        Assert.Contains("Declined",   all);
        Assert.Contains("Cancelled",  all);

        // Scheduled is now legacy — must NOT appear in canonical All list
        Assert.DoesNotContain("Scheduled", all);
        // Other legacy values must NOT be in canonical All list
        Assert.DoesNotContain("Received",  all);
        Assert.DoesNotContain("Contacted", all);
    }

    // ── Legacy.Normalize maps old values correctly ────────────────────────────
    [Theory]
    [InlineData("Received",   "Accepted")]
    [InlineData("Contacted",  "Accepted")]
    [InlineData("Scheduled",  "InProgress")]  // LSCC-01-001-01: Scheduled → InProgress
    [InlineData("New",        "New")]
    [InlineData("Accepted",   "Accepted")]
    [InlineData("InProgress", "InProgress")]
    [InlineData("Declined",   "Declined")]
    public void Legacy_Normalize_MapsExpected(string input, string expected)
    {
        Assert.Equal(expected, Referral.ValidStatuses.Legacy.Normalize(input));
    }

    // ── ValidateTransition throws for invalid transitions ─────────────────────
    [Fact]
    public void ValidateTransition_InvalidTransition_Throws()
    {
        Assert.Throws<BuildingBlocks.Exceptions.ValidationException>(
            () => ReferralWorkflowRules.ValidateTransition("Completed", "New"));
    }

    [Fact]
    public void ValidateTransition_AcceptedToCompleted_Throws()
    {
        // LSCC-01-001-01: Accepted → Completed is blocked; must go through InProgress.
        Assert.Throws<BuildingBlocks.Exceptions.ValidationException>(
            () => ReferralWorkflowRules.ValidateTransition("Accepted", "Completed"));
    }

    [Fact]
    public void ValidateTransition_SameStatus_DoesNotThrow()
    {
        var ex = Record.Exception(
            () => ReferralWorkflowRules.ValidateTransition("Accepted", "Accepted"));
        Assert.Null(ex);
    }

    [Fact]
    public void ValidateTransition_InProgressToCancelled_DoesNotThrow()
    {
        // LSCC-01-001-01: InProgress → Cancelled must be allowed.
        var ex = Record.Exception(
            () => ReferralWorkflowRules.ValidateTransition("InProgress", "Cancelled"));
        Assert.Null(ex);
    }
}
