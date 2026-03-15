using System;
using System.Linq;
using System.Threading;
using MatrixApi;

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

        void EnqueueNextScene()
        {
            var specialScene = isTestMode
                ? sceneSelector.GetNextSceneInCycle()
                : sceneSelector.GetScene();
            scene.SpecialScenes.Enqueue(specialScene);
            Console.WriteLine($"Enqueued scene: {specialScene.Name}");
        }

        scene.NewSceneWanted += (_, _) => EnqueueNextScene();

        if (isTestMode) EnqueueNextScene();

        var keepRunning = true;
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            keepRunning = false;
        };

        RGBLedMatrix? matrix = null;
        RGBLedCanvas? canvas = null;
        ConsoleMatrixSimulator? simulator = null;

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
    }
}
