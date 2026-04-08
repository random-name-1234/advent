using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace advent;

internal sealed class WebControlHostedService(
    SceneControlService sceneControl,
    SceneRenderer sceneRenderer,
    MatrixFramePresenter framePresenter,
    WebControlOptions options) : IHostedService
{
    private WebApplication? app;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            Console.WriteLine("Control web UI disabled via ADVENT_WEB_ENABLED.");
            return Task.CompletedTask;
        }

        app = ControlWebHost.Build(sceneControl, sceneRenderer, framePresenter, options);
        return app.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (app is null)
            return;

        try
        {
            await app.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
