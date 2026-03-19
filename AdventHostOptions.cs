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
            MatrixWidth: 64,
            MatrixHeight: 32,
            HardwareFrameDelayMs: 20,
            SimulatorFrameDelayMs: 66,
            IsTestMode: args.Any(static arg => string.Equals(arg, "--test-mode", StringComparison.OrdinalIgnoreCase)),
            Month: DateTime.Now.Month,
            ImageSceneDirectory: "advent-images");
    }
}
