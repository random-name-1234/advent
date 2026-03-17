using System;
using System.Linq;
using MatrixApi;
using Pi5MatrixSharp;

namespace advent;

internal sealed record MatrixOutputOptions(
    MatrixBackend Backend,
    RGBLedMatrixOptions Pi4Options,
    Pi5MatrixOptions Pi5Options)
{
    public static MatrixOutputOptions FromEnvironmentAndArgs(string[] args, int width, int height)
    {
        var backend = ResolveBackend(args);
        var pi4Options = new RGBLedMatrixOptions
        {
            ChainLength = 1,
            HardwareMapping = ReadString("ADVENT_PI4_HARDWARE_MAPPING", "adafruit-hat-pwm"),
            Rows = height,
            Cols = width
        };

        var pi5Options = new Pi5MatrixOptions
        {
            Colorspace = ReadEnum("ADVENT_PI5_COLORSPACE", Pi5Colorspace.Rgb888Packed),
            Pinout = ReadEnum("ADVENT_PI5_PINOUT", Pi5MatrixPinout.AdafruitMatrixBonnet),
            Geometry = new Pi5MatrixGeometryOptions
            {
                Width = width,
                Height = height,
                AddressLineCount = ReadInt("ADVENT_PI5_ADDR_LINES", 4),
                Serpentine = ReadBool("ADVENT_PI5_SERPENTINE", true),
                Orientation = ReadEnum("ADVENT_PI5_ORIENTATION", Pi5MatrixOrientation.Normal),
                PlaneCount = ReadInt("ADVENT_PI5_PLANES", 10),
                TemporalPlaneCount = ReadInt("ADVENT_PI5_TEMPORAL_PLANES", 2)
            }
        };

        return new MatrixOutputOptions(backend, pi4Options, pi5Options);
    }

    private static MatrixBackend ResolveBackend(string[] args)
    {
        if (args.Any(static arg => string.Equals(arg, "--simulator", StringComparison.OrdinalIgnoreCase)))
            return MatrixBackend.Simulator;

        var explicitBackendArg = args
            .FirstOrDefault(static arg => arg.StartsWith("--backend=", StringComparison.OrdinalIgnoreCase));
        if (explicitBackendArg is not null)
        {
            var raw = explicitBackendArg["--backend=".Length..];
            if (TryParseBackend(raw, out var parsed))
                return parsed;
        }

        var envBackend = Environment.GetEnvironmentVariable("ADVENT_MATRIX_BACKEND");
        if (TryParseBackend(envBackend, out var fromEnv))
            return fromEnv;

        return MatrixBackend.Pi4;
    }

    private static bool TryParseBackend(string? raw, out MatrixBackend backend)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "pi4":
                backend = MatrixBackend.Pi4;
                return true;
            case "pi5":
                backend = MatrixBackend.Pi5;
                return true;
            case "sim":
            case "simulator":
                backend = MatrixBackend.Simulator;
                return true;
            default:
                backend = default;
                return false;
        }
    }

    private static bool ReadBool(string envName, bool defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (bool.TryParse(raw, out var parsed))
            return parsed;

        return raw.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }

    private static int ReadInt(string envName, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        return int.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static string ReadString(string envName, string defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        return string.IsNullOrWhiteSpace(raw) ? defaultValue : raw.Trim();
    }

    private static TEnum ReadEnum<TEnum>(string envName, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        return Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) ? parsed : defaultValue;
    }
}
