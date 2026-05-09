using System.Threading;
using System.Threading.Tasks;

namespace FPS.SharedKernel.Interfaces
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}