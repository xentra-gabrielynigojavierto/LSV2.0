using Xunit;
using Comms.Application.DTOs;
using Comms.Application.Services;
using Comms.Domain.Entities;
using Comms.Domain.Enums;
using Comms.Infrastructure.Persistence;
using Comms.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Comms.Tests;

public class OperationalViewTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid User1 = Guid.NewGuid();
    private static readonly Guid User2 = Guid.NewGuid();
    private static readonly Guid User3 = Guid.NewGuid();

    private static CommsDbContext CreateDb(string? name = null) =>
        TestHelpers.CreateDbContext(name ?? Guid.NewGuid().ToString());

    private static async Task<Conversation> SeedConversation(
        CommsDbContext db, string subject, string status = "Open", Guid? tenantId = null)
    {
        var conv = Conversation.Create(
            tenantId ?? TenantId, OrgId, "SYNQ_COMMS",
            ContextType.Case, $"case-{Guid.NewGuid():N}",
            subject, VisibilityType.InternalOnly, User1);

        if (status != "New")
        {
            conv.AutoTransitionToOpen(User1);
        }

        db.Conversations.Add(conv);
        await db.SaveChangesAsync();
        return conv;
    }

    private static async Task<ConversationAssignment> SeedAssignment(
        CommsDbContext db, Guid conversationId, Guid? assignedUserId, Guid? queueId = null, string status = "Assigned")
    {
        var assignment = ConversationAssignment.Create(
            TenantId, conversationId, queueId, assignedUserId, User1, User1);

        db.ConversationAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }

    private static async Task<ConversationSlaState> SeedSla(
        CommsDbContext db, Guid conversationId, string priority = "Normal",
        bool breachFirst = false, bool breachResolution = false)
    {
        var sla = ConversationSlaState.Initialize(TenantId, conversationId, priority, DateTime.UtcNow.AddHours(-1), User1);
        if (breachFirst)
        {
            var pastDue = DateTime.UtcNow.AddHours(-2);
            sla.EvaluateBreaches(DateTime.UtcNow);
        }
        db.ConversationSlaStates.Add(sla);
        await db.SaveChangesAsync();

        if (breachFirst || breachResolution)
        {
            var entry = db.Entry(sla);
            if (breachFirst) entry.Property(s => s.BreachedFirstResponse).CurrentValue = true;
            if (breachResolution) entry.Property(s => s.BreachedResolution).CurrentValue = true;
            await db.SaveChangesAsync();
        }

        return sla;
    }

    private static async Task SeedMention(
        CommsDbContext db, Guid conversationId, Guid mentionedUserId, Guid? messageId = null)
    {
        var mention = MessageMention.Create(
            TenantId, conversationId,
            messageId ?? Guid.NewGuid(),
            mentionedUserId, User1, true);
        db.MessageMentions.Add(mention);
        await db.SaveChangesAsync();
    }

    private static async Task SeedReadState(
        CommsDbContext db, Guid conversationId, Guid userId, bool isRead = true)
    {
        var readState = ConversationReadState.Create(
            TenantId, conversationId, userId, Guid.NewGuid(), userId);
        db.ConversationReadStates.Add(readState);
        await db.SaveChangesAsync();

        if (isRead)
        {
            readState.MarkRead(Guid.NewGuid(), userId);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedMessage(
        CommsDbContext db, Guid conversationId, string body)
    {
        var msg = Message.Create(
            conversationId, TenantId, OrgId,
            Channel.InApp, Direction.Internal,
            body, VisibilityType.InternalOnly,
            User1, senderUserId: User1,
            senderParticipantType: ParticipantType.InternalUser);
        db.Messages.Add(msg);
        await db.SaveChangesAsync();
    }

    private static async Task<ConversationQueue> SeedQueue(CommsDbContext db, string name = "Support")
    {
        var queue = ConversationQueue.Create(TenantId, name, name.ToLower().Replace(" ", "-"), $"{name} queue", false, User1);
        db.ConversationQueues.Add(queue);
        await db.SaveChangesAsync();
        return queue;
    }

    [Fact]
    public async Task FilterByAssignedUserId_ReturnsOnlyAssignedConversations()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "Assigned to User1");
        var conv2 = await SeedConversation(db, "Assigned to User2");
        await SeedAssignment(db, conv1.Id, User1);
        await SeedAssignment(db, conv2.Id, User2);

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(AssignedUserId: User1);

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(conv1.Id, items[0].ConversationId);
    }

    [Fact]
    public async Task FilterByQueueId_ReturnsOnlyQueueConversations()
    {
        var db = CreateDb();
        var queue = await SeedQueue(db);
        var conv1 = await SeedConversation(db, "In queue");
        var conv2 = await SeedConversation(db, "Not in queue");
        await SeedAssignment(db, conv1.Id, User1, queue.Id);
        await SeedAssignment(db, conv2.Id, User1);

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(QueueId: queue.Id);

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal(1, total);
        Assert.Equal("In queue", items[0].Subject);
        Assert.Equal(queue.Name, items[0].QueueName);
    }

    [Fact]
    public async Task FilterBySlaBreach_ReturnsBreachedConversations()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "Breached");
        var conv2 = await SeedConversation(db, "Not breached");
        await SeedSla(db, conv1.Id, breachFirst: true);
        await SeedSla(db, conv2.Id);

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(BreachedFirstResponse: true);

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal(1, total);
        Assert.Equal("Breached", items[0].Subject);
        Assert.True(items[0].BreachedFirstResponse);
    }

    [Fact]
    public async Task FilterByPriority_ReturnsMatchingConversations()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "High priority");
        var conv2 = await SeedConversation(db, "Normal priority");
        await SeedSla(db, conv1.Id, priority: "High");
        await SeedSla(db, conv2.Id, priority: "Normal");

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(Priority: "High");

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal(1, total);
        Assert.Equal("High priority", items[0].Subject);
    }

    [Fact]
    public async Task FilterByMentionedUserId_ReturnsConversationsWithMentions()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "Has mention");
        var conv2 = await SeedConversation(db, "No mention");
        await SeedMention(db, conv1.Id, User2);

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(MentionedUserId: User2);

        var (items, total) = await repo.QueryAsync(TenantId, request, User2);

        Assert.Equal(1, total);
        Assert.Equal("Has mention", items[0].Subject);
    }

    [Fact]
    public async Task FilterByUnreadOnly_ReturnsUnreadConversations()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "Unread");
        var conv2 = await SeedConversation(db, "Read");
        await SeedReadState(db, conv2.Id, User1, isRead: true);

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(UnreadOnly: true);

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Contains(items, i => i.Subject == "Unread");
        Assert.True(items.All(i => i.IsUnread));
    }

    [Fact]
    public async Task CombinedFilters_ComposeCorrectly()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "Match all");
        var conv2 = await SeedConversation(db, "Only assigned");
        var conv3 = await SeedConversation(db, "Only breached");

        await SeedAssignment(db, conv1.Id, User1);
        await SeedAssignment(db, conv2.Id, User1);
        await SeedSla(db, conv1.Id, priority: "High", breachFirst: true);
        await SeedSla(db, conv3.Id, breachFirst: true);

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(
            AssignedUserId: User1,
            BreachedFirstResponse: true);

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal(1, total);
        Assert.Equal("Match all", items[0].Subject);
    }

    [Fact]
    public async Task Pagination_WorksCorrectly()
    {
        var db = CreateDb();
        for (int i = 1; i <= 5; i++)
        {
            await SeedConversation(db, $"Conv {i}");
            await Task.Delay(10);
        }

        var repo = new OperationalConversationQueryRepository(db);

        var page1 = new OperationalQueryRequest(Page: 1, PageSize: 2);
        var (items1, total1) = await repo.QueryAsync(TenantId, page1, User1);
        Assert.Equal(5, total1);
        Assert.Equal(2, items1.Count);

        var page2 = new OperationalQueryRequest(Page: 2, PageSize: 2);
        var (items2, total2) = await repo.QueryAsync(TenantId, page2, User1);
        Assert.Equal(5, total2);
        Assert.Equal(2, items2.Count);

        var page3 = new OperationalQueryRequest(Page: 3, PageSize: 2);
        var (items3, total3) = await repo.QueryAsync(TenantId, page3, User1);
        Assert.Equal(1, items3.Count);

        var allIds = items1.Concat(items2).Concat(items3).Select(i => i.ConversationId).ToList();
        Assert.Equal(5, allIds.Distinct().Count());
    }

    [Fact]
    public async Task Sorting_DefaultDescByLastActivity()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "First");
        await Task.Delay(20);
        var conv2 = await SeedConversation(db, "Second");

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest();

        var (items, _) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal(2, items.Count);
        Assert.Equal("Second", items[0].Subject);
        Assert.Equal("First", items[1].Subject);
    }

    [Fact]
    public async Task Sorting_AscByCreatedAt()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "First");
        await Task.Delay(20);
        var conv2 = await SeedConversation(db, "Second");

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(SortBy: "createdAtUtc", SortDirection: "asc");

        var (items, _) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal("First", items[0].Subject);
        Assert.Equal("Second", items[1].Subject);
    }

    [Fact]
    public async Task TenantIsolation_CrossTenantRecordsNeverAppear()
    {
        var db = CreateDb();
        await SeedConversation(db, "My tenant");
        await SeedConversation(db, "Other tenant", tenantId: OtherTenantId);

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest();

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal(1, total);
        Assert.Equal("My tenant", items[0].Subject);

        var (otherItems, otherTotal) = await repo.QueryAsync(OtherTenantId, request, User1);
        Assert.Equal(1, otherTotal);
        Assert.Equal("Other tenant", otherItems[0].Subject);
    }

    [Fact]
    public async Task MentionCount_ReturnsCorrectCountForCurrentUser()
    {
        var db = CreateDb();
        var conv = await SeedConversation(db, "Mentioned");
        await SeedMention(db, conv.Id, User1);
        await SeedMention(db, conv.Id, User1, Guid.NewGuid());
        await SeedMention(db, conv.Id, User2);

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest();

        var (items, _) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Single(items);
        Assert.Equal(2, items[0].MentionCount);
    }

    [Fact]
    public async Task LastMessageSnippet_ReturnsLatestMessageBody()
    {
        var db = CreateDb();
        var conv = await SeedConversation(db, "With messages");
        await SeedMessage(db, conv.Id, "First message");
        await Task.Delay(10);
        await SeedMessage(db, conv.Id, "Latest message");

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest();

        var (items, _) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal("Latest message", items[0].LastMessageSnippet);
    }

    [Fact]
    public async Task OperationalViewService_ClampsPagination()
    {
        var db = CreateDb();
        await SeedConversation(db, "Test");

        var repo = new OperationalConversationQueryRepository(db);
        var service = new OperationalViewService(repo, TestHelpers.CreateLogger<OperationalViewService>());

        var request = new OperationalQueryRequest(Page: -1, PageSize: 9999);
        var result = await service.QueryConversationsAsync(TenantId, User1, request);

        Assert.Equal(1, result.Page);
        Assert.Equal(200, result.PageSize);
    }

    [Fact]
    public async Task OperationalViewService_HasMoreFlag()
    {
        var db = CreateDb();
        for (int i = 0; i < 3; i++)
            await SeedConversation(db, $"Conv {i}");

        var repo = new OperationalConversationQueryRepository(db);
        var service = new OperationalViewService(repo, TestHelpers.CreateLogger<OperationalViewService>());

        var request = new OperationalQueryRequest(Page: 1, PageSize: 2);
        var result = await service.QueryConversationsAsync(TenantId, User1, request);

        Assert.True(result.HasMore);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        var request2 = new OperationalQueryRequest(Page: 2, PageSize: 2);
        var result2 = await service.QueryConversationsAsync(TenantId, User1, request2);

        Assert.False(result2.HasMore);
        Assert.Single(result2.Items);
    }

    [Fact]
    public async Task RegressionTest_ExistingOperationalListStillWorks()
    {
        var db = CreateDb();
        var conv = await SeedConversation(db, "Test conv");
        await SeedAssignment(db, conv.Id, User1);
        await SeedSla(db, conv.Id, priority: "High");

        var operationalService = TestHelpers.CreateOperationalService(db);
        var query = new OperationalListQuery(Priority: "High");
        var result = await operationalService.ListOperationalAsync(TenantId, query);

        Assert.Single(result);
        Assert.Equal(conv.Id, result[0].ConversationId);
    }

    [Fact]
    public async Task FilterByOperationalStatus_ReturnsMatchingConversations()
    {
        var db = CreateDb();
        var conv1 = await SeedConversation(db, "Open conv", status: "Open");
        var conv2 = await SeedConversation(db, "New conv", status: "New");

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest(OperationalStatus: "Open");

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Equal(1, total);
        Assert.Equal("Open conv", items[0].Subject);
    }

    [Fact]
    public async Task NoResults_ReturnsEmptyWithZeroTotal()
    {
        var db = CreateDb();

        var repo = new OperationalConversationQueryRepository(db);
        var request = new OperationalQueryRequest();

        var (items, total) = await repo.QueryAsync(TenantId, request, User1);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }
}
