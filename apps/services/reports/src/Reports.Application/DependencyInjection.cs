using Microsoft.Extensions.DependencyInjection;
using Reports.Application.Assignments;
using Reports.Application.Execution;
using Reports.Application.Export;
using Reports.Application.Guardrails;
using Reports.Application.Overrides;
using Reports.Application.Scheduling;
using Reports.Application.Templates;
using Reports.Application.Views;
using Reports.Contracts.Guardrails;

namespace Reports.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddReportsApplication(this IServiceCollection services)
    {
        services.AddSingleton<IGuardrailValidator, GuardrailValidator>();
        services.AddScoped<ITemplateManagementService, TemplateManagementService>();
        services.AddScoped<ITemplateAssignmentService, TemplateAssignmentService>();
        services.AddScoped<ITenantReportOverrideService, TenantReportOverrideService>();
        services.AddScoped<IReportExecutionService, ReportExecutionService>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IReportScheduleService, ReportScheduleService>();
        services.AddScoped<ITenantReportViewService, TenantReportViewService>();

        return services;
    }
}
