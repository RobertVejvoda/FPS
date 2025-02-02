using FPS.Core.Domain;

namespace FPS.Core.Repository;

public abstract class RepositoryBase<T> where T : IAggregateRoot
{
    public abstract Task AddAsync(T domainObject);
}