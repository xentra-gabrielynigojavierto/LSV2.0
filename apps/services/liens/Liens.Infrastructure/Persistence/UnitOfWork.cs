using Liens.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Liens.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly LiensDbContext _db;

    public UnitOfWork(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _db.Database.BeginTransactionAsync(ct);
        return new EfTransactionScope(transaction);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    private sealed class EfTransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _transaction;

        public EfTransactionScope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken ct = default)
            => _transaction.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default)
            => _transaction.RollbackAsync(ct);

        public ValueTask DisposeAsync()
            => _transaction.DisposeAsync();
    }
}
