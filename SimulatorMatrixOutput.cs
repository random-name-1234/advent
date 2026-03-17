using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed class SimulatorMatrixOutput : IMatrixOutput
{
    private readonly ConsoleMatrixSimulator simulator;

    public SimulatorMatrixOutput(int width, int height)
    {
        simulator = new ConsoleMatrixSimulator(width, height);
    }

    public string Name => "SIMULATOR";

    public void Present(Image<Rgba32> frame)
    {
        simulator.Render(frame);
    }

    public void Dispose()
    {
        simulator.Dispose();
    }
}
