using Comms.Domain.Enums;
using Xunit;

namespace Comms.Tests;

public class StatusTransitionTests
{
    [Theory]
    [InlineData("New", "Open", true)]
    [InlineData("Open", "PendingInternal", true)]
    [InlineData("Open", "PendingExternal", true)]
    [InlineData("Open", "Resolved", true)]
    [InlineData("Open", "Closed", true)]
    [InlineData("PendingInternal", "Open", true)]
    [InlineData("PendingExternal", "Open", true)]
    [InlineData("Resolved", "Closed", true)]
    [InlineData("Resolved", "Open", true)]
    [InlineData("Closed", "Open", true)]
    [InlineData("Closed", "Archived", true)]
    [InlineData("New", "Closed", false)]
    [InlineData("New", "Resolved", false)]
    [InlineData("New", "PendingInternal", false)]
    [InlineData("Open", "Archived", false)]
    [InlineData("Closed", "Resolved", false)]
    [InlineData("Archived", "Open", false)]
    [InlineData("Archived", "Closed", false)]
    public void StatusTransition_ValidatesCorrectly(string from, string to, bool expectedValid)
    {
        Assert.Equal(expectedValid, ConversationStatus.IsValidTransition(from, to));
    }

    [Fact]
    public void UpdateStatus_ValidTransition_Succeeds()
    {
        var conversation = TestHelpers.CreateTestConversation();
        conversation.UpdateStatus(ConversationStatus.Open, TestHelpers.UserId1);
        Assert.Equal(ConversationStatus.Open, conversation.Status);
    }

    [Fact]
    public void UpdateStatus_InvalidTransition_ThrowsInvalidOperationException()
    {
        var conversation = TestHelpers.CreateTestConversation();
        Assert.Throws<InvalidOperationException>(() =>
            conversation.UpdateStatus(ConversationStatus.Closed, TestHelpers.UserId1));
    }
}
