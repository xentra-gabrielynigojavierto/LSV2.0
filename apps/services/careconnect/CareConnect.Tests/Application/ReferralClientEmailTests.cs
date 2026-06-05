// LSCC-01-002: Client Acceptance Email Tests
// Covers: NotificationType registration, NotificationRecipientType.ClientEmail,
// acceptance email recipient-type contract, and client-email skip behaviour.
using CareConnect.Application.Services;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-01-002 — Verifies that the client acceptance email is wired correctly at the
/// domain / notification-type layer and that the email service handles a missing
/// ClientEmail gracefully (no exception, acceptance not blocked).
///
///   NotificationType:
///     - ReferralAcceptedClient constant exists with the correct string value
///     - ReferralAcceptedClient is included in All (so DB validators accept it)
///     - IsValid returns true for ReferralAcceptedClient
///
///   NotificationRecipientType:
///     - ClientEmail is in All
///
///   ReferralEmailService — acceptance confirmation:
///     - When ClientEmail is present, a notification with type ReferralAcceptedClient
///       and recipientType ClientEmail is persisted before the SMTP attempt
///     - When ClientEmail is empty, no notification is persisted and no exception is thrown
///       (acceptance is never blocked by a missing client email)
/// </summary>
public class ReferralClientEmailTests
{
    private const string TestSecret  = "TEST-CLIENT-EMAIL-SECRET-2026";
    private const string TestBaseUrl = "http://localhost:3000";

    // ── NotificationType domain checks ────────────────────────────────────────

    [Fact]
    public void NotificationType_ReferralAcceptedClient_HasExpectedStringValue()
    {
        Assert.Equal("ReferralAcceptedClient", NotificationType.ReferralAcceptedClient);
    }

    [Fact]
    public void NotificationType_All_ContainsReferralAcceptedClient()
    {
        Assert.Contains(NotificationType.ReferralAcceptedClient, NotificationType.All);
    }

    [Fact]
    public void NotificationType_IsValid_ReturnsTrueForReferralAcceptedClient()
    {
        Assert.True(NotificationType.IsValid(NotificationType.ReferralAcceptedClient));
    }

    // ── NotificationRecipientType domain checks ───────────────────────────────

    [Fact]
    public void NotificationRecipientType_All_ContainsClientEmail()
    {
        Assert.Contains(NotificationRecipientType.ClientEmail, NotificationRecipientType.All);
    }

    [Fact]
    public void NotificationRecipientType_IsValid_ReturnsTrueForClientEmail()
    {
        Assert.True(NotificationRecipientType.IsValid(NotificationRecipientType.ClientEmail));
    }

    // ── Acceptance notification recipient-type contract ───────────────────────

    [Fact]
    public void NotificationType_AllThreeAcceptanceTypes_ArePresentAndDistinct()
    {
        // Provider, Referrer, and Client are three separate notification types —
        // each maps to a different audience and recipient address.
        var providerType = NotificationType.ReferralAcceptedProvider;
        var referrerType = NotificationType.ReferralAcceptedReferrer;
        var clientType   = NotificationType.ReferralAcceptedClient;

        Assert.True(NotificationType.IsValid(providerType));
        Assert.True(NotificationType.IsValid(referrerType));
        Assert.True(NotificationType.IsValid(clientType));

        // All three must be distinct strings.
        Assert.NotEqual(providerType, referrerType);
        Assert.NotEqual(providerType, clientType);
        Assert.NotEqual(referrerType, clientType);
    }

    // ── ReferralEmailService: acceptance confirmation with client email ────────

    [Fact]
    public async Task SendAcceptanceConfirmationsAsync_WithClientEmail_PersistsClientNotification()
    {
        // Arrange
        var notifRepo = new Mock<INotificationRepository>();
        var producer  = new Mock<INotificationsProducer>();

        // Producer throws so we can test that the notification record was still persisted.
        producer.Setup(p => p.SubmitAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notifications service unavailable in test"));

        var svc = BuildEmailService(notifRepo.Object, producer.Object);

        var referral = BuildReferral(clientEmail: "client@example.com");
        var provider = BuildProvider(email: "provider@example.org");

        // Act — should not throw even if SMTP fails
        await svc.SendAcceptanceConfirmationsAsync(referral, provider);

        // Assert — at least one call to AddAsync with type ReferralAcceptedClient
        notifRepo.Verify(r => r.AddAsync(
            It.Is<CareConnectNotification>(n =>
                n.NotificationType == NotificationType.ReferralAcceptedClient &&
                n.RecipientType    == NotificationRecipientType.ClientEmail   &&
                n.RecipientAddress == "client@example.com"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAcceptanceConfirmationsAsync_WithoutClientEmail_DoesNotPersistClientNotification()
    {
        // Arrange
        var notifRepo = new Mock<INotificationRepository>();
        var producer  = new Mock<INotificationsProducer>();

        var svc      = BuildEmailService(notifRepo.Object, producer.Object);
        var referral = BuildReferral(clientEmail: "");   // empty → skip
        var provider = BuildProvider(email: "provider@example.org");

        // Act — must not throw
        var ex = await Record.ExceptionAsync(() =>
            svc.SendAcceptanceConfirmationsAsync(referral, provider));

        Assert.Null(ex);

        // Assert — no ReferralAcceptedClient notification record created
        notifRepo.Verify(r => r.AddAsync(
            It.Is<CareConnectNotification>(n =>
                n.NotificationType == NotificationType.ReferralAcceptedClient),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAcceptanceConfirmationsAsync_WithClientEmail_SendsToClientAddress()
    {
        // Arrange
        var notifRepo = new Mock<INotificationRepository>();
        var producer  = new Mock<INotificationsProducer>();

        var svc      = BuildEmailService(notifRepo.Object, producer.Object);
        var referral = BuildReferral(clientEmail: "client@lawcase.com");
        var provider = BuildProvider(email: "provider@clinic.com");

        // Act
        await svc.SendAcceptanceConfirmationsAsync(referral, provider);

        // Assert — producer was called with the client's address
        producer.Verify(p => p.SubmitAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            "client@lawcase.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAcceptanceConfirmationsAsync_WithAllEmails_SendsThreeEmails()
    {
        // Provider, Referrer, and Client all have addresses → three SMTP sends.
        var notifRepo = new Mock<INotificationRepository>();
        var producer  = new Mock<INotificationsProducer>();

        var svc = BuildEmailService(notifRepo.Object, producer.Object);

        var referral = BuildReferral(
            clientEmail:  "client@example.com",
            referrerEmail: "lawfirm@example.com");
        var provider = BuildProvider(email: "provider@clinic.com");

        await svc.SendAcceptanceConfirmationsAsync(referral, provider);

        producer.Verify(p => p.SubmitAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ReferralEmailService BuildEmailService(
        INotificationRepository notifRepo,
        INotificationsProducer  producer)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReferralToken:Secret"] = TestSecret,
                ["AppBaseUrl"]           = TestBaseUrl,
            })
            .Build();

        return new ReferralEmailService(
            notifRepo,
            producer,
            config,
            new Mock<ITenantServiceClient>().Object,
            NullLogger<ReferralEmailService>.Instance);
    }

    private static Referral BuildReferral(
        string clientEmail   = "client@example.com",
        string? referrerEmail = null)
    {
        var r = Referral.Create(
            tenantId:                  Guid.NewGuid(),
            referringOrganizationId:   null,
            receivingOrganizationId:   null,
            providerId:                Guid.NewGuid(),
            subjectPartyId:            null,
            subjectNameSnapshot:       null,
            subjectDobSnapshot:        null,
            clientFirstName:           "Jane",
            clientLastName:            "Doe",
            clientDob:                 null,
            clientPhone:               "555-000-0001",
            clientEmail:               clientEmail,
            caseNumber:                null,
            requestedService:          "Physical Therapy",
            urgency:                   Referral.ValidUrgencies.Normal,
            notes:                     null,
            createdByUserId:           null,
            organizationRelationshipId: null,
            referrerEmail:             referrerEmail,
            referrerName:              null);
        return r;
    }

    private static Provider BuildProvider(string email = "provider@clinic.com")
        => Provider.Create(
            tenantId:          Guid.NewGuid(),
            name:              "Test Clinic",
            organizationName:  "Test Clinic LLC",
            email:             email,
            phone:             "555-000-9999",
            addressLine1:      "123 Main St",
            city:              "Las Vegas",
            state:             "NV",
            postalCode:        "89101",
            isActive:          true,
            acceptingReferrals: true,
            createdByUserId:   null);
}
