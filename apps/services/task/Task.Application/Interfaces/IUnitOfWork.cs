namespace Task.Application.Interfaces;

public interface IUnitOfWork
{
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}
