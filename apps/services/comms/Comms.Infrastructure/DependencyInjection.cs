using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Context;
using BuildingBlocks.Notifications;
using LegalSynq.AuditClient;
using Comms.Application.Interfaces;
using Comms.Application.Repositories;
using Comms.Application.Services;
using Comms.Infrastructure.Audit;
using Comms.Infrastructure.Documents;
using Comms.Infrastructure.Notifications;
using Comms.Infrastructure.Persistence;
using Comms.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCommsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SynqCommDb")
            ?? throw new InvalidOperationException("Connection string 'SynqCommDb' is not configured.");

        services.AddDbContext<CommsDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        services.AddScoped<IConversationReadStateRepository, ConversationReadStateRepository>();
        services.AddScoped<IMessageAttachmentRepository, MessageAttachmentRepository>();
        services.AddScoped<IEmailMessageReferenceRepository, EmailMessageReferenceRepository>();
        services.AddScoped<IExternalParticipantIdentityRepository, ExternalParticipantIdentityRepository>();
        services.AddScoped<IEmailDeliveryStateRepository, EmailDeliveryStateRepository>();
        services.AddScoped<IEmailRecipientRecordRepository, EmailRecipientRecordRepository>();
        services.AddScoped<ITenantEmailSenderConfigRepository, TenantEmailSenderConfigRepository>();
        services.AddScoped<IEmailTemplateConfigRepository, EmailTemplateConfigRepository>();
        services.AddScoped<IConversationQueueRepository, ConversationQueueRepository>();
        services.AddScoped<IConversationAssignmentRepository, ConversationAssignmentRepository>();
        services.AddScoped<IConversationSlaStateRepository, ConversationSlaStateRepository>();
        services.AddScoped<IConversationSlaTriggerStateRepository, ConversationSlaTriggerStateRepository>();
        services.AddScoped<IQueueEscalationConfigRepository, QueueEscalationConfigRepository>();
        services.AddScoped<IConversationTimelineRepository, ConversationTimelineRepository>();
        services.AddScoped<IMessageMentionRepository, MessageMentionRepository>();
        services.AddScoped<IOperationalConversationQueryRepository, OperationalConversationQueryRepository>();

        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IReadTrackingService, ReadTrackingService>();
        services.AddScoped<IMessageAttachmentService, MessageAttachmentService>();
        services.AddScoped<IEmailIntakeService, EmailIntakeService>();
        services.AddScoped<IOutboundEmailService, OutboundEmailService>();
        services.AddScoped<ISenderConfigService, SenderConfigService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IQueueService, QueueService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IOperationalService, OperationalService>();
        services.AddScoped<IEscalationTargetResolver, EscalationTargetResolver>();
        services.AddScoped<ISlaNotificationService, SlaNotificationService>();
        services.AddScoped<IQueueEscalationConfigService, QueueEscalationConfigService>();
        services.AddScoped<IConversationTimelineService, ConversationTimelineService>();
        services.AddScoped<IMentionService, MentionService>();
        services.AddScoped<IOperationalViewService, OperationalViewService>();

        // LS-NOTIF-CORE-021 — service token issuer for Notifications calls.
        services.AddServiceTokenIssuer(configuration, "comms-service");
        services.AddTransient<NotificationsAuthDelegatingHandler>();

        var notifBaseUrl = configuration["Services:NotificationsUrl"] ?? "http://localhost:5008";
        services.AddHttpClient("NotificationsService", client =>
        {
            client.BaseAddress = new Uri(notifBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<NotificationsAuthDelegatingHandler>();
        services.AddScoped<INotificationsServiceClient, NotificationsServiceClient>();

        var docsBaseUrl = configuration["Services:DocumentsUrl"] ?? "http://localhost:5006";
        services.AddHttpClient("DocumentsService", client =>
        {
            client.BaseAddress = new Uri(docsBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IDocumentServiceClient, DocumentServiceClient>();

        services.AddAuditEventClient(configuration);
        services.AddScoped<IAuditPublisher, AuditPublisher>();

        return services;
    }
}
