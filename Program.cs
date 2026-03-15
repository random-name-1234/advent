using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MatrixApi;
using SixLabors.ImageSharp.PixelFormats;

namespace advent
{
    class Program
    { 
        private const int MatrixWidth = 64;
        private const int MatrixHeight = 32;
        private const int FrameDelayMs = 20;

        static void Main(string[] args)
        {
            var isTestMode = args.Any(static arg => string.Equals(arg, "--test-mode", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine("Starting meerkats...");
            Console.WriteLine(isTestMode ? "Scene mode: TEST (cycle all scenes)." : "Scene mode: NORMAL (random seasonal scenes).");

            var matrix = new RGBLedMatrix(new RGBLedMatrixOptions
            {
                ChainLength = 1,
                HardwareMapping = "adafruit-hat-pwm",
                Rows = 32,
                Cols = 64
            });

            var canvas = matrix.CreateOffscreenCanvas();


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

            scene.NewSceneWanted += (s, e) =>
            {
                EnqueueNextScene();
            };

            if (isTestMode)
            {
                EnqueueNextScene();
            }

            var previousFrame = new Rgba32[MatrixWidth * MatrixHeight];
            var hasPreviousFrame = false;
            var previousTimestamp = Stopwatch.GetTimestamp();
            var stopwatchFrequency = Stopwatch.Frequency;


            while (true)

            {
                var nowTimestamp = Stopwatch.GetTimestamp();
                var elapsed = TimeSpan.FromSeconds((nowTimestamp - previousTimestamp) / (double)stopwatchFrequency);
                previousTimestamp = nowTimestamp;
                scene.Elapsed(elapsed);

                var frame = scene.Img;
                for (var y = 0; y < MatrixHeight; y++)
                {
                    var rowOffset = y * MatrixWidth;
                    for (var x = 0; x < MatrixWidth; x++)
                    {
                        var pixel = frame[x, y];
                        var index = rowOffset + x;
                        var previous = previousFrame[index];
                        if (hasPreviousFrame &&
                            previous.R == pixel.R &&
                            previous.G == pixel.G &&
                            previous.B == pixel.B)
                        {
                            continue;
                        }

                        previousFrame[index] = pixel;
                        canvas.SetPixel(x, y, new Color(pixel.R, pixel.G, pixel.B));
                    }
                }
                hasPreviousFrame = true;

                matrix.SwapOnVsync(canvas);
                Thread.Sleep(FrameDelayMs);
            }
        }
    }
}
