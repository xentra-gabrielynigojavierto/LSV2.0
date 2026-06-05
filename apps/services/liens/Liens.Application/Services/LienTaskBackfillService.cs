using System.Diagnostics;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// TASK-B04 — paginated, idempotent backfill of all Liens task rows into the
/// canonical Task service.  Uses the ExternalId approach: each LienTask.Id is
/// passed as the ExternalId so Liens task IDs and Task service IDs are identical.
/// </summary>
public sealed class LienTaskBackfillService : ILienTaskBackfillService
{
    private readonly ILienTaskRepository     _taskRepo;
    private readonly ILienTaskNoteRepository _noteRepo;
    private readonly ILiensTaskServiceClient _taskClient;
    private readonly ILogger<LienTaskBackfillService> _logger;

    public LienTaskBackfillService(
        ILienTaskRepository              taskRepo,
        ILienTaskNoteRepository          noteRepo,
        ILiensTaskServiceClient          taskClient,
        ILogger<LienTaskBackfillService> logger)
    {
        _taskRepo   = taskRepo;
        _noteRepo   = noteRepo;
        _taskClient = taskClient;
        _logger     = logger;
    }

    public async Task<LienTaskBackfillReport> RunAsync(
        Guid              actingAdminUserId,
        int               batchSize = 100,
        CancellationToken ct        = default)
    {
        var sw           = Stopwatch.StartNew();
        int attempted    = 0;
        int created      = 0;
        int alreadyExist = 0;
        int failed       = 0;
        int totalNotes   = 0;
        int totalLinks   = 0;
        int page         = 1;

        _logger.LogInformation(
            "LienTaskBackfill: starting — batchSize={BatchSize} adminUser={AdminUser}",
            batchSize, actingAdminUserId);

        while (true)
        {
            var tasks = await _taskRepo.GetAllPagedAsync(page, batchSize, ct);

            if (tasks.Count == 0) break;

            foreach (var task in tasks)
            {
                attempted++;

                try
                {
                    var notes = await _noteRepo.GetByTaskIdAsync(task.TenantId, task.Id, ct);
                    var links = await _taskRepo.GetLienLinksForTaskAsync(task.Id, ct);
                    var lienIds = links.Select(l => l.LienId).ToList();

                    var noteProjections = notes
                        .Select(n => (n.Id, n.Content, n.CreatedByName, n.CreatedByUserId, n.CreatedAtUtc))
                        .ToList();

                    var request = new CreateTaskRequest
                    {
                        Title                = task.Title,
                        Description          = task.Description,
                        Priority             = task.Priority,
                        AssignedUserId       = task.AssignedUserId,
                        CaseId               = task.CaseId,
                        LienIds              = lienIds,
                        WorkflowStageId      = task.WorkflowStageId,
                        DueDate              = task.DueDate,
                        SourceType           = task.SourceType,
                        GenerationRuleId     = task.GenerationRuleId,
                        GeneratingTemplateId = task.GeneratingTemplateId,
                    };

                    var result = await _taskClient.BackfillTaskAsync(
                        task.TenantId,
                        actingAdminUserId,
                        task.Id,
                        request,
                        noteProjections,
                        [],
                        ct);

                    if (result.AlreadyExisted)
                    {
                        alreadyExist++;
                    }
                    else
                    {
                        created++;
                        totalNotes += result.NotesCreated;
                        totalLinks += result.LinksCreated;
                    }

                    // If task had a non-NEW status, transition it in the Task service
                    if (!string.Equals(task.Status, "NEW", StringComparison.OrdinalIgnoreCase)
                        && !result.AlreadyExisted)
                    {
                        try
                        {
                            await _taskClient.TransitionStatusAsync(
                                task.TenantId, result.TaskId, actingAdminUserId, task.Status, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "LienTaskBackfill: could not set status '{Status}' on task {TaskId}",
                                task.Status, result.TaskId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex,
                        "LienTaskBackfill: failed to backfill task {TaskId} tenant={TenantId}",
                        task.Id, task.TenantId);
                }
            }

            if (tasks.Count < batchSize) break;
            page++;
        }

        sw.Stop();

        _logger.LogInformation(
            "LienTaskBackfill: complete — attempted={A} created={C} alreadyExisted={E} failed={F} " +
            "notes={N} links={L} elapsed={Elapsed}",
            attempted, created, alreadyExist, failed, totalNotes, totalLinks, sw.Elapsed);

        return new LienTaskBackfillReport(attempted, created, alreadyExist, failed,
            totalNotes, totalLinks, sw.Elapsed);
    }
}
