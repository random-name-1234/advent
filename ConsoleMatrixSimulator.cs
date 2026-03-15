using System;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public sealed class ConsoleMatrixSimulator : IDisposable
{
    private readonly StringBuilder frameBuilder;
    private readonly int height;
    private readonly int width;
    private bool disposed;
    private bool firstFrame = true;

    public ConsoleMatrixSimulator(int width, int height)
    {
        this.width = width;
        this.height = height;
        frameBuilder = new StringBuilder(width * height * 24);
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        Console.Write("\x1b[0m\x1b[?25h");
    }

    public void Render(Image<Rgba32> frame)
    {
        if (disposed) throw new ObjectDisposedException(nameof(ConsoleMatrixSimulator));

        if (firstFrame)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Write("\x1b[2J\x1b[?25l");
            firstFrame = false;
        }

        frameBuilder.Clear();
        frameBuilder.Append("\x1b[H");
        frameBuilder.AppendLine("Simulator Mode (Ctrl+C to exit)");

        for (var y = 0; y < height; y += 2)
        {
            for (var x = 0; x < width; x++)
            {
                var top = frame[x, y];
                var bottom = y + 1 < height ? frame[x, y + 1] : default;
                AppendHalfBlock(top, bottom);
            }

            frameBuilder.Append("\x1b[0m");
            frameBuilder.AppendLine();
        }

        Console.Write(frameBuilder.ToString());
    }

    private void AppendHalfBlock(Rgba32 top, Rgba32 bottom)
    {
        frameBuilder.Append("\x1b[38;2;")
            .Append(top.R).Append(';')
            .Append(top.G).Append(';')
            .Append(top.B).Append('m');

        frameBuilder.Append("\x1b[48;2;")
            .Append(bottom.R).Append(';')
            .Append(bottom.G).Append(';')
            .Append(bottom.B).Append('m');

        frameBuilder.Append('▀');
    }
}