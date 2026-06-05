using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Interfaces;
using Flow.Application.Services;
using Flow.Domain.Interfaces;
using Flow.Infrastructure.Persistence;
using Flow.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOW_DB_CONNECTION_STRING")
            ?? configuration.GetConnectionString("FlowDb")
            ?? "Server=localhost;Database=flow_db;User=root;Password=;";

        services.AddDbContext<FlowDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)),
                mysqlOptions => mysqlOptions.EnableRetryOnFailure(3)));

        services.AddScoped<IFlowDbContext>(provider => provider.GetRequiredService<FlowDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));

        // TASK-FLOW-01 / TASK-FLOW-02 — Task service HTTP clients.
        //
        // Two clients are registered:
        //   1. Typed client (user-bearer auth): for all user-facing Task service endpoints
        //      (AuthenticatedUser policy). Forwards the caller's bearer token.
        //   2. Named client "FlowTaskInternal" (service-token auth): for internal endpoints
        //      (InternalService policy: flow-sla-update, flow-queue-assign).
        //      Always mints a service token via X-Tenant-Id header.
        services.AddHttpContextAccessor();
        services.AddTransient<Flow.Infrastructure.TaskService.FlowTaskServiceAuthDelegatingHandler>();
        services.AddTransient<Flow.Infrastructure.TaskService.FlowTaskInternalAuthHandler>();

        var taskBaseUrl = configuration["ExternalServices:Task:BaseUrl"] ?? "http://localhost:5016";

        services.AddHttpClient<IFlowTaskServiceClient, Flow.Infrastructure.TaskService.FlowTaskServiceClient>(client =>
        {
            client.BaseAddress = new Uri(taskBaseUrl);
            client.Timeout     = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<Flow.Infrastructure.TaskService.FlowTaskServiceAuthDelegatingHandler>();

        // TASK-FLOW-02 — internal named client (service token, no user bearer forwarding).
        services.AddHttpClient("FlowTaskInternal", client =>
        {
            client.BaseAddress = new Uri(taskBaseUrl);
            client.Timeout     = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<Flow.Infrastructure.TaskService.FlowTaskInternalAuthHandler>();

        return services;
    }

    public static IServiceCollection AddTenantProvider<TTenantProvider>(this IServiceCollection services)
        where TTenantProvider : class, ITenantProvider
    {
        services.AddScoped<ITenantProvider, TTenantProvider>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITaskService, Flow.Application.Services.TaskService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IAutomationExecutor, AutomationExecutor>();
        services.AddScoped<INotificationService, NotificationService>();
        // LS-FLOW-MERGE-P3 — product-facing service for SynqLien/CareConnect/SynqFund.
        services.AddScoped<IProductWorkflowService, ProductWorkflowService>();
        // LS-FLOW-MERGE-P5 — execution authority for WorkflowInstance.
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        // LS-FLOW-E11.2 — auto-creates WorkflowTask rows from workflow
        // transitions. Stateless service; one per scoped DbContext.
        services.AddScoped<IWorkflowTaskFromWorkflowFactory, WorkflowTaskFromWorkflowFactory>();
        // LS-FLOW-E11.3 — deterministic assignment resolver. Stateless,
        // no DB reads, no external calls; safe as a singleton but
        // registered scoped to match the rest of Flow.Application
        // services and keep the lifetime story uniform.
        services.AddScoped<IWorkflowTaskAssignmentResolver, StaticRuleWorkflowTaskAssignmentResolver>();
        // LS-FLOW-E11.4 — task lifecycle (start / complete / cancel).
        // Stateless service; uses the scoped IFlowDbContext for the
        // atomic compare-and-swap UPDATE. Lifetime intentionally aligns
        // with the rest of Flow.Application so a single DI scope per
        // request is the unit of work.
        services.AddScoped<IWorkflowTaskLifecycleService, WorkflowTaskLifecycleService>();
        // LS-FLOW-E11.5 — read-only "My Tasks" query surface for the
        // calling user. Scoped so it shares the per-request IFlowDbContext
        // and IFlowUserContext, both of which are themselves scoped.
        services.AddScoped<IMyTasksService, MyTasksService>();
        // LS-FLOW-E11.7 — task completion ↔ workflow progression binding.
        // Composes IWorkflowTaskLifecycleService and IWorkflowEngine inside
        // a single transaction acquired through the DB execution strategy
        // so the persisted outcome is "task Completed AND workflow advanced"
        // or nothing.
        services.AddScoped<IWorkflowTaskCompletionService, WorkflowTaskCompletionService>();
        // LS-FLOW-E14.2 — sole entry point for user-driven assignment
        // transitions (claim / reassign). Stateless; uses the scoped
        // IFlowDbContext for the atomic CAS UPDATE and IAuditAdapter
        // for fire-and-forget audit emission.
        services.AddScoped<IWorkflowTaskAssignmentService, WorkflowTaskAssignmentService>();
        // LS-FLOW-E18 — work distribution intelligence layer.
        // WorkloadService: active task counts per user (single GROUP BY query).
        // TaskRecommendationService: deterministic, explainable recommendation engine.
        // Both are scoped to the request so they share the per-request
        // IFlowDbContext (tenant filter + transaction safety).
        services.AddScoped<IWorkloadService, WorkloadService>();
        services.AddScoped<ITaskRecommendationService, TaskRecommendationService>();

        // E19 — analytics read model service. Scoped to share the per-request
        // IFlowDbContext (tenant filter + AsNoTracking aggregation queries).
        services.AddScoped<IFlowAnalyticsService, FlowAnalyticsService>();

        return services;
    }
}
