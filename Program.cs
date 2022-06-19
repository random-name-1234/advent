using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using MatrixApi;
using Microsoft.Extensions.Hosting;
using System.Net.Sockets;
using System.IO;
using System.Globalization;

namespace advent
{
    class Program
    { 

        static void Main(string[] args)
        {
            Console.WriteLine("Starting meerkats...");

            var matrix = new RGBLedMatrix(new RGBLedMatrixOptions
            {
                ChainLength = 1,
                HardwareMapping = "adafruit-hat-pwm",
                Rows = 32,
                Cols = 64
            });

            var canvas = matrix.CreateOffscreenCanvas();


            var scene = new Scene();

            var sceneSelector = new SceneSelector();
            scene.NewSceneWanted += (s, e) =>
            {
                var specialScene = sceneSelector.GetScene();
                scene.SpecialScenes.Enqueue(specialScene);
                Console.WriteLine($"Enqueued scene: {specialScene.Name}");
            };

            bool canReadKeys = Console.IsInputRedirected;

            var now = DateTime.UtcNow;
            var startedAt = now;
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
            Console.WriteLine("Exiting meerkats.");
        }
    }
}
