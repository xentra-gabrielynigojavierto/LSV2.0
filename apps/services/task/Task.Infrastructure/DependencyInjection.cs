using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Context;
using BuildingBlocks.Notifications;
using LegalSynq.AuditClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Task.Application.Interfaces;
using Task.Application.Repositories;
using Task.Application.Services;
using Task.Infrastructure.Persistence;
using Task.Infrastructure.Persistence.Repositories;
using Task.Infrastructure.Services;

namespace Task.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskServices(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        var connectionString = configuration.GetConnectionString("TasksDb")
            ?? throw new InvalidOperationException(
                "Connection string 'TasksDb' is not configured. " +
                "Set it via the environment variable 'ConnectionStrings__TasksDb'.");

        services.AddDbContext<TasksDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        // Core task repositories
        services.AddScoped<ITaskRepository,             TaskRepository>();
        services.AddScoped<ITaskNoteRepository,         TaskNoteRepository>();
        services.AddScoped<ITaskHistoryRepository,      TaskHistoryRepository>();
        services.AddScoped<ITaskLinkedEntityRepository, TaskLinkedEntityRepository>();
        services.AddScoped<IUnitOfWork,                 UnitOfWork>();

        // Execution engine repositories
        services.AddScoped<ITaskStageRepository,           TaskStageRepository>();
        services.AddScoped<ITaskStageTransitionRepository, TaskStageTransitionRepository>();
        services.AddScoped<ITaskGovernanceRepository,      TaskGovernanceRepository>();
        services.AddScoped<ITaskTemplateRepository,        TaskTemplateRepository>();
        services.AddScoped<ITaskReminderRepository,        TaskReminderRepository>();

        // Application services
        services.AddScoped<ITaskGovernanceService,      TaskGovernanceService>();
        services.AddScoped<ITaskStageService,           TaskStageService>();
        services.AddScoped<ITaskStageTransitionService, TaskStageTransitionService>();
        services.AddScoped<ITaskTemplateService,        TaskTemplateService>();
        services.AddScoped<ITaskReminderService,        TaskReminderService>();
        services.AddScoped<ITaskService,                TaskService>();

        // TASK-FLOW-04 — analytics service + repository
        services.AddScoped<ITaskAnalyticsRepository, TaskAnalyticsRepository>();
        services.AddScoped<ITaskAnalyticsService,    TaskAnalyticsService>();
        services.AddScoped<ITaskWorkloadRepository,  TaskWorkloadRepository>();
        services.AddScoped<ITaskWorkloadService,     TaskWorkloadService>();

        // Audit integration — LegalSynq.AuditClient pattern
        services.AddAuditEventClient(configuration);
        services.AddScoped<ITaskAuditPublisher, TaskAuditPublisher>();

        // Notification client — LS-NOTIF-CORE-024 pattern
        services.AddOptions<TaskNotificationsServiceOptions>()
                .Bind(configuration.GetSection(TaskNotificationsServiceOptions.SectionName));

        services.AddServiceTokenIssuer(configuration, "task");
        services.AddTransient<NotificationsAuthDelegatingHandler>();

        services.AddHttpClient("TaskNotificationsService")
                .AddHttpMessageHandler<NotificationsAuthDelegatingHandler>();

        services.AddScoped<ITaskNotificationClient, TaskNotificationClient>();

        // TASK-B05 (TASK-015) — monitoring service registration on startup
        services.AddOptions<TaskMonitoringOptions>()
                .Bind(configuration.GetSection(TaskMonitoringOptions.SectionName));
        services.AddHostedService<TaskServiceRegistrar>();

        return services;
    }
}
