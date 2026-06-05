using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

public class AppointmentWorkflowRulesTests
{
    // ── Canonical status validation ───────────────────────────────────────────
    [Theory]
    [InlineData("Pending",     true)]
    [InlineData("Confirmed",   true)]
    [InlineData("Completed",   true)]
    [InlineData("Cancelled",   true)]
    [InlineData("Rescheduled", true)]
    [InlineData("NoShow",      true)]
    [InlineData("Scheduled",   true)]   // legacy alias still accepted
    [InlineData("Unknown",     false)]
    public void IsValidStatus_ReturnsExpected(string status, bool expected)
    {
        Assert.Equal(expected, AppointmentWorkflowRules.IsValidStatus(status));
    }

    // ── Reschedulable states ──────────────────────────────────────────────────
    [Theory]
    [InlineData("Pending",     true)]
    [InlineData("Confirmed",   true)]
    [InlineData("Scheduled",   true)]   // legacy alias is reschedulable
    [InlineData("Completed",   false)]
    [InlineData("Cancelled",   false)]
    [InlineData("Rescheduled", false)]
    public void IsReschedulable_ReturnsExpected(string status, bool expected)
    {
        Assert.Equal(expected, AppointmentWorkflowRules.IsReschedulable(status));
    }

    // ── Terminal states ───────────────────────────────────────────────────────
    [Theory]
    [InlineData("Completed",  true)]
    [InlineData("Cancelled",  true)]
    [InlineData("NoShow",     true)]
    [InlineData("Pending",    false)]
    [InlineData("Confirmed",  false)]
    [InlineData("Rescheduled",false)]
    public void IsTerminal_ReturnsExpected(string status, bool expected)
    {
        Assert.Equal(expected, AppointmentWorkflowRules.IsTerminal(status));
    }

    // ── Canonical transitions ─────────────────────────────────────────────────
    [Theory]
    [InlineData("Pending",     "Confirmed",   true)]
    [InlineData("Pending",     "Rescheduled", true)]
    [InlineData("Pending",     "Cancelled",   true)]
    [InlineData("Pending",     "NoShow",      true)]
    [InlineData("Confirmed",   "Completed",   true)]
    [InlineData("Confirmed",   "Rescheduled", true)]
    [InlineData("Confirmed",   "Cancelled",   true)]
    [InlineData("Rescheduled", "Pending",     true)]
    [InlineData("Rescheduled", "Confirmed",   true)]
    [InlineData("Rescheduled", "Cancelled",   true)]
    [InlineData("Completed",   "Cancelled",   false)]
    [InlineData("Cancelled",   "Pending",     false)]
    [InlineData("NoShow",      "Confirmed",   false)]
    public void IsValidTransition_ReturnsExpected(string from, string to, bool expected)
    {
        Assert.Equal(expected, AppointmentWorkflowRules.IsValidTransition(from, to));
    }

    // ── ValidateStatus throws for unknown status ──────────────────────────────
    [Fact]
    public void ValidateStatus_UnknownStatus_Throws()
    {
        Assert.Throws<BuildingBlocks.Exceptions.ValidationException>(
            () => AppointmentWorkflowRules.ValidateStatus("Unknown"));
    }

    [Fact]
    public void ValidateStatus_KnownStatus_DoesNotThrow()
    {
        var ex = Record.Exception(
            () => AppointmentWorkflowRules.ValidateStatus("Pending"));
        Assert.Null(ex);
    }
}
