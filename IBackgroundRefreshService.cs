using System.Threading;
using System.Threading.Tasks;

namespace advent;

internal interface IBackgroundRefreshService
{
    Task RunAsync(CancellationToken cancellationToken);
}
