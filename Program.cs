using System;
using System.Linq;
using System.Threading;
using MatrixApi;
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
        var isSimulatorMode =
            args.Any(static arg => string.Equals(arg, "--simulator", StringComparison.OrdinalIgnoreCase));

        Console.WriteLine("Starting meerkats...");
        Console.WriteLine(isSimulatorMode ? "Output mode: SIMULATOR" : "Output mode: LED MATRIX");
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

        RGBLedMatrix? matrix = null;
        RGBLedCanvas? canvas = null;
        ConsoleMatrixSimulator? simulator = null;
        WebApplication? webApp = null;

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

        if (isSimulatorMode)
        {
            simulator = new ConsoleMatrixSimulator(MatrixWidth, MatrixHeight);
        }
        else
        {
            matrix = new RGBLedMatrix(new RGBLedMatrixOptions
            {
                ChainLength = 1,
                HardwareMapping = "adafruit-hat-pwm",
                Rows = MatrixHeight,
                Cols = MatrixWidth
            });
            canvas = matrix.CreateOffscreenCanvas();
        }

        var now = DateTime.UtcNow;
        var prev = now;
        var elapsed = now - prev;

        while (keepRunning)
        {
            now = DateTime.UtcNow;
            elapsed = now - prev;
            scene.Elapsed(elapsed);

            if (simulator is not null)
            {
                simulator.Render(scene.Img);
            }
            else if (canvas is not null && matrix is not null)
            {
                canvas.Clear();

                for (var y = 0; y < MatrixHeight; y++)
                for (var x = 0; x < MatrixWidth; x++)
                {
                    var pixel = scene.Img[x, y];
                    canvas.SetPixel(x, y, new Color(pixel.R, pixel.G, pixel.B));
                }

                matrix.SwapOnVsync(canvas);
            }

            prev = now;
            Thread.Sleep(isSimulatorMode ? SimulatorFrameDelayMs : HardwareFrameDelayMs);
        }

        simulator?.Dispose();
        matrix?.Dispose();
        if (webApp is not null)
        {
            webApp.StopAsync().GetAwaiter().GetResult();
            webApp.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
