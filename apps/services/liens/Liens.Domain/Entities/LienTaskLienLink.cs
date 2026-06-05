namespace Liens.Domain.Entities;

public class LienTaskLienLink
{
    public Guid TaskId    { get; private set; }
    public Guid LienId    { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId  { get; private set; }

    private LienTaskLienLink() { }

    public static LienTaskLienLink Create(Guid taskId, Guid lienId, Guid createdByUserId)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("TaskId is required.", nameof(taskId));
        if (lienId == Guid.Empty) throw new ArgumentException("LienId is required.", nameof(lienId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        return new LienTaskLienLink
        {
            TaskId          = taskId,
            LienId          = lienId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc    = DateTime.UtcNow,
        };
    }
}
