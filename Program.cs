﻿using System;
using System.Linq;
using System.Threading;
using MatrixApi;

namespace advent
{
    class Program
    { 

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

            var now = DateTime.UtcNow;
            var prev = now;
            var elapsed = now - prev;


            while (true)

            {
                canvas.Clear();
                now = DateTime.UtcNow;
                elapsed = now - prev;
                scene.Elapsed(elapsed);

                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        canvas.SetPixel(x, y, new Color(
                            (int)scene.Img[x, y].R,
                            (int)scene.Img[x, y].G,
                            (int)scene.Img[x, y].B));
                    }
                }

                prev = now;

                matrix.SwapOnVsync(canvas);
                Thread.Sleep(20);
            }
        }
    }
}
