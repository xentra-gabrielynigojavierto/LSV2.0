using Task.Application.Interfaces;

namespace Task.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly TasksDbContext _db;
    public UnitOfWork(TasksDbContext db) => _db = db;

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
