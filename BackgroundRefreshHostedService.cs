using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace advent;

internal sealed class BackgroundRefreshHostedService<TRefreshService>(TRefreshService refreshService)
    : BackgroundService
    where TRefreshService : class, IBackgroundRefreshService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return refreshService.RunAsync(stoppingToken);
    }
}
