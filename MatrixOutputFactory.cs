using System;

namespace advent;

internal static class MatrixOutputFactory
{
    public static IMatrixOutput Create(MatrixOutputOptions options, int width, int height)
    {
        return options.Backend switch
        {
            MatrixBackend.Simulator => new SimulatorMatrixOutput(width, height),
            MatrixBackend.Pi4 => new Pi4MatrixOutput(options.Pi4Options),
            MatrixBackend.Pi5 => new Pi5MatrixOutput(options.Pi5Options),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Backend, "Unsupported matrix backend.")
        };
    }
}
