using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

public static class TaskReminderEndpoints
{
    public static void MapTaskReminderEndpoints(this WebApplication app)
    {
        // Admin-protected cron endpoint — called by external scheduler
        app.MapPost("/api/tasks/reminders/process", ProcessReminders)
            .RequireAuthorization(Policies.AdminOnly)
            .WithTags("TaskReminders");
    }

    private static async System.Threading.Tasks.Task<IResult> ProcessReminders(
        ITaskReminderService reminderService,
        int               batchSize = 100,
        CancellationToken ct        = default)
    {
        var result = await reminderService.ProcessDueRemindersAsync(batchSize, ct);
        return Results.Ok(result);
    }
}
