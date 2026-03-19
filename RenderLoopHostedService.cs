using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace advent;

internal sealed class RenderLoopHostedService(
    AdventHostOptions hostOptions,
    MatrixOutputOptions outputOptions,
    SceneControlService sceneControl,
    SceneRenderer sceneRenderer,
    IMatrixOutput output)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isSimulatorMode = outputOptions.Backend == MatrixBackend.Simulator;

        Console.WriteLine("Starting meerkats...");
        Console.WriteLine($"Output mode: {outputOptions.Backend.ToString().ToUpperInvariant()}");
        Console.WriteLine(hostOptions.IsTestMode
            ? "Scene mode: TEST (cycle all scenes)."
            : "Scene mode: NORMAL (random seasonal scenes).");
        Console.WriteLine($"Matrix backend initialized: {output.Name}");

        var previous = DateTime.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var elapsed = now - previous;
            previous = now;

            sceneControl.Advance(elapsed);
            sceneRenderer.AdvanceAndRender(elapsed, sceneControl.ActiveScene);
            output.Present(sceneRenderer.Img);

            var delay = isSimulatorMode ? hostOptions.SimulatorFrameDelayMs : hostOptions.HardwareFrameDelayMs;
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }
}
