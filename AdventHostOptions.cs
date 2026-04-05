using System;
using System.Linq;

namespace advent;

internal sealed record AdventHostOptions(
    int MatrixWidth,
    int MatrixHeight,
    int HardwareFrameDelayMs,
    int SimulatorFrameDelayMs,
    bool IsTestMode,
    int Month,
    string ImageSceneDirectory)
{
    public static AdventHostOptions FromArgs(string[] args)
    {
        return new AdventHostOptions(
            MatrixWidth: ReadInt(args, "--matrix-width", "ADVENT_MATRIX_WIDTH", 64),
            MatrixHeight: ReadInt(args, "--matrix-height", "ADVENT_MATRIX_HEIGHT", 32),
            HardwareFrameDelayMs: 20,
            SimulatorFrameDelayMs: 66,
            IsTestMode: args.Any(static arg => string.Equals(arg, "--test-mode", StringComparison.OrdinalIgnoreCase)),
            Month: DateTime.Now.Month,
            ImageSceneDirectory: "advent-images");
    }

    private static int ReadInt(string[] args, string argPrefix, string envName, int defaultValue)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith(argPrefix + "=", StringComparison.OrdinalIgnoreCase));
        if (arg is not null && int.TryParse(arg[(argPrefix.Length + 1)..], out var fromArg))
            return fromArg;

        var env = Environment.GetEnvironmentVariable(envName);
        return int.TryParse(env, out var fromEnv) ? fromEnv : defaultValue;
    }
}
