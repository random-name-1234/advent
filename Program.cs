using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;

namespace advent;

internal class Program
{
    private const int MatrixWidth = 64;
    private const int MatrixHeight = 32;
    private const int HardwareFrameDelayMs = 20;
    private const int SimulatorFrameDelayMs = 66;

    private static void Main(string[] args)
    {
        var isTestMode = args.Any(static arg => string.Equals(arg, "--test-mode", StringComparison.OrdinalIgnoreCase));
        var outputOptions = MatrixOutputOptions.FromEnvironmentAndArgs(args, MatrixWidth, MatrixHeight);
        var isSimulatorMode = outputOptions.Backend == MatrixBackend.Simulator;

        Console.WriteLine("Starting meerkats...");
        Console.WriteLine($"Output mode: {outputOptions.Backend.ToString().ToUpperInvariant()}");
        Console.WriteLine(isTestMode
            ? "Scene mode: TEST (cycle all scenes)."
            : "Scene mode: NORMAL (random seasonal scenes).");

        var scene = new Scene
        {
            ContinuousSceneRequests = isTestMode
        };

        var sceneSelector = new SceneSelector();
        var sceneControl = new SceneControlService(scene, sceneSelector, isTestMode);
        scene.NewSceneWanted += (_, _) => sceneControl.EnqueueNextScene();
        if (isTestMode) sceneControl.EnqueueNextScene();

        var keepRunning = true;
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            keepRunning = false;
        };

        IMatrixOutput? output = null;
        WebApplication? webApp = null;

        try
        {
            var webControlOptions = WebControlOptions.FromEnvironment();
            if (webControlOptions.Enabled)
            {
                webApp = ControlWebHost.Build(sceneControl, webControlOptions);
                webApp.StartAsync().GetAwaiter().GetResult();
            }
            else
            {
                Console.WriteLine("Control web UI disabled via ADVENT_WEB_ENABLED.");
            }

            output = MatrixOutputFactory.Create(outputOptions, MatrixWidth, MatrixHeight);
            Console.WriteLine($"Matrix backend initialized: {output.Name}");

            var now = DateTime.UtcNow;
            var prev = now;

            while (keepRunning)
            {
                now = DateTime.UtcNow;
                var elapsed = now - prev;
                scene.Elapsed(elapsed);

                output.Present(scene.Img);

                prev = now;
                Thread.Sleep(isSimulatorMode ? SimulatorFrameDelayMs : HardwareFrameDelayMs);
            }
        }
        finally
        {
            output?.Dispose();
            if (webApp is not null)
            {
                try
                {
                    webApp.StopAsync().GetAwaiter().GetResult();
                }
                finally
                {
                    webApp.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
        }
    }
}
