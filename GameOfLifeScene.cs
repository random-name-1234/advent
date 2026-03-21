using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class GameOfLifeScene : ISpecialScene
{
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);
    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(120);
    private readonly byte[,] ages = new byte[Width, Height];

    private readonly Random random = new();
    private bool[,] current = new bool[Width, Height];

    private TimeSpan elapsedThisScene;
    private bool[,] next = new bool[Width, Height];
    private TimeSpan simulationAccumulator;
    private int stableGenerations;

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }

    public bool RainbowSnow => false;

    public string Name => "Game of Life";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        simulationAccumulator = TimeSpan.Zero;
        stableGenerations = 0;
        HidesTime = true;
        IsActive = true;
        SeedGrid();
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive) return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene > SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
            return;
        }

        simulationAccumulator += timeSpan;
        while (simulationAccumulator >= StepInterval)
        {
            simulationAccumulator -= StepInterval;
            Step();
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            if (!current[x, y]) continue;

            var intensity = (byte)Math.Min(255, 56 + ages[x, y] * 20);
            img[x, y] = new Rgba32((byte)(intensity / 6), intensity, (byte)(intensity / 3));
        }
    }

    private void SeedGrid()
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            current[x, y] = random.NextDouble() < 0.35;
            ages[x, y] = current[x, y] ? (byte)1 : (byte)0;
        }
    }

    private void Step()
    {
        var changed = false;

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var alive = current[x, y];
            var neighbours = CountNeighbours(x, y);
            var nextAlive = alive
                ? neighbours is 2 or 3
                : neighbours == 3;

            next[x, y] = nextAlive;
            ages[x, y] = nextAlive
                ? alive
                    ? (byte)Math.Min(10, ages[x, y] + 1)
                    : (byte)1
                : (byte)0;

            changed |= nextAlive != alive;
        }

        var old = current;
        current = next;
        next = old;

        if (changed)
        {
            stableGenerations = 0;
            return;
        }

        stableGenerations++;
        if (stableGenerations >= 20)
        {
            stableGenerations = 0;
            SeedGrid();
        }
    }

    private int CountNeighbours(int x, int y)
    {
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;

            var nx = (x + dx + Width) % Width;
            var ny = (y + dy + Height) % Height;
            if (current[nx, ny]) count++;
        }

        return count;
    }
}