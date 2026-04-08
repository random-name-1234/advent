using System;
using System.Linq;

namespace advent;

internal sealed record AdventHostOptions(
    MatrixProfile MatrixProfile,
    int HardwareFrameDelayMs,
    int SimulatorFrameDelayMs,
    bool IsTestMode,
    int Month,
    string ImageSceneDirectory)
{
    public int MatrixWidth => MatrixProfile.Width;

    public int MatrixHeight => MatrixProfile.Height;

    public static AdventHostOptions FromArgs(string[] args) => FromArgs(args, name => Environment.GetEnvironmentVariable(name));

    internal static AdventHostOptions FromArgs(string[] args, Func<string, string?> readEnvironment)
    {
        var matrixProfile = MatrixProfile.FromConfiguration(ReadMatrixSize(args, readEnvironment));

        return new AdventHostOptions(
            MatrixProfile: matrixProfile,
            HardwareFrameDelayMs: 20,
            SimulatorFrameDelayMs: 66,
            IsTestMode: args.Any(static arg => string.Equals(arg, "--test-mode", StringComparison.OrdinalIgnoreCase)),
            Month: DateTime.Now.Month,
            ImageSceneDirectory: "advent-images");
    }

    private static string? ReadMatrixSize(string[] args, Func<string, string?> readEnvironment)
    {
        var explicitArg = args
            .FirstOrDefault(static arg => arg.StartsWith("--matrix-size=", StringComparison.OrdinalIgnoreCase));
        if (explicitArg is not null)
            return explicitArg["--matrix-size=".Length..];

        return readEnvironment("ADVENT_MATRIX_SIZE");
    }
}
