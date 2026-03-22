using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class TetrisScene : ISpecialScene
{
    private const int Cols = 10;
    private const int Rows = 28;
    private const int CellW = 2;    // each cell is 2px wide, 1px tall
    private const int CellH = 1;
    private const int WellPixelW = Cols * CellW;  // 20px
    private const int OffsetX = (Width - WellPixelW) / 2;  // 22, centered
    private const int OffsetY = 2;
    private const float SimulationStepSeconds = 1f / 30f;

    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(20);

    private static readonly int[][][] Pieces =
    [
        // I
        [
            [0,0, 1,0, 2,0, 3,0],
            [0,0, 0,1, 0,2, 0,3]
        ],
        // O
        [
            [0,0, 1,0, 0,1, 1,1]
        ],
        // T
        [
            [0,0, 1,0, 2,0, 1,1],
            [0,0, 0,1, 0,2, 1,1],
            [1,0, 0,1, 1,1, 2,1],
            [1,0, 1,1, 1,2, 0,1]
        ],
        // S
        [
            [1,0, 2,0, 0,1, 1,1],
            [0,0, 0,1, 1,1, 1,2]
        ],
        // Z
        [
            [0,0, 1,0, 1,1, 2,1],
            [1,0, 1,1, 0,1, 0,2]
        ],
        // L
        [
            [0,0, 0,1, 0,2, 1,2],
            [0,0, 1,0, 2,0, 0,1],
            [0,0, 1,0, 1,1, 1,2],
            [2,0, 0,1, 1,1, 2,1]
        ],
        // J
        [
            [1,0, 1,1, 1,2, 0,2],
            [0,0, 0,1, 1,1, 2,1],
            [0,0, 1,0, 0,1, 0,2],
            [0,0, 1,0, 2,0, 2,1]
        ]
    ];

    private static readonly Rgba32[] PieceColors =
    [
        new(0, 240, 240),   // I - cyan
        new(240, 240, 0),   // O - yellow
        new(160, 0, 240),   // T - purple
        new(0, 240, 0),     // S - green
        new(240, 0, 0),     // Z - red
        new(240, 160, 0),   // L - orange
        new(0, 0, 240)      // J - blue
    ];

    private readonly Random random = new();
    private readonly int[,] grid = new int[Cols, Rows]; // 0 = empty, 1..7 = piece color index+1
    private readonly List<int> pieceBag = new(14);

    private float simulationAccumulator;
    private TimeSpan elapsedThisScene;
    private float dropAccumulator;
    private float dropInterval;
    private int currentPiece;
    private int currentRotation;
    private int currentX;
    private int currentY;
    private bool hasCurrent;
    private int linesCleared;

    // Line clear animation state
    private readonly HashSet<int> flashingRows = new();
    private float flashTimer;
    private const float FlashDuration = 0.25f;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Tetris";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        simulationAccumulator = 0f;
        dropAccumulator = 0f;
        dropInterval = 1f / 6f;
        linesCleared = 0;
        hasCurrent = false;
        flashTimer = 0f;
        flashingRows.Clear();
        pieceBag.Clear();
        IsActive = true;
        HidesTime = true;
        Array.Clear(grid, 0, grid.Length);
        PrePopulateGrid();
    }

    private void PrePopulateGrid()
    {
        // Fill the bottom portion with a realistic-looking Tetris landscape
        // so the scene looks interesting from the very first frame.
        var fillHeight = 8 + random.Next(5); // 8-12 rows from bottom
        for (var y = Rows - fillHeight; y < Rows; y++)
        {
            // Each row gets a random number of filled cells (not full — leave gaps)
            var fillCount = 6 + random.Next(3); // 6-8 out of 10
            var columns = new List<int>();
            for (var x = 0; x < Cols; x++) columns.Add(x);

            // Fisher-Yates to pick random columns
            for (var i = columns.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (columns[i], columns[j]) = (columns[j], columns[i]);
            }

            for (var i = 0; i < fillCount; i++)
                grid[columns[i], y] = random.Next(1, 8); // random piece color
        }
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

        simulationAccumulator += Math.Clamp((float)timeSpan.TotalSeconds, 0f, 0.25f);
        while (simulationAccumulator >= SimulationStepSeconds)
        {
            simulationAccumulator -= SimulationStepSeconds;
            UpdateSimulation(SimulationStepSeconds);
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        DrawBackground(img);
        DrawBorder(img);
        DrawGrid(img);

        if (hasCurrent)
            DrawPiece(img, currentPiece, currentRotation, currentX, currentY, PieceColors[currentPiece]);
    }

    private void UpdateSimulation(float dt)
    {
        // Handle line clear flash animation
        if (flashingRows.Count > 0)
        {
            flashTimer -= dt;
            if (flashTimer <= 0f)
            {
                CollapseFlashingRows();
                flashingRows.Clear();
            }
            return;
        }

        if (!hasCurrent)
        {
            SpawnPiece();
            if (!hasCurrent)
                return;

            // Immediately place via AI
            ChooseBestPlacement(out var bestRot, out var bestX);
            currentRotation = bestRot;
            currentX = bestX;
        }

        // Drop piece
        dropAccumulator += dt;
        while (dropAccumulator >= dropInterval)
        {
            dropAccumulator -= dropInterval;

            if (CanPlace(currentPiece, currentRotation, currentX, currentY + 1))
            {
                currentY++;
            }
            else
            {
                LockPiece();
                CheckLines();

                // Speed up slightly over time
                dropInterval = MathF.Max(1f / 16f, 1f / 6f - linesCleared * 0.003f);
            }
        }
    }

    private void SpawnPiece()
    {
        if (pieceBag.Count == 0)
            RefillBag();

        currentPiece = pieceBag[^1];
        pieceBag.RemoveAt(pieceBag.Count - 1);
        currentRotation = 0;
        currentX = Cols / 2 - 2;
        currentY = 0;
        dropAccumulator = 0f;

        if (!CanPlace(currentPiece, currentRotation, currentX, currentY))
        {
            // Board full, reset
            Array.Clear(grid, 0, grid.Length);
            linesCleared = 0;
            dropInterval = 1f / 6f;
            hasCurrent = false;
            return;
        }

        hasCurrent = true;
    }

    private void RefillBag()
    {
        // 7-bag randomizer: add all 7 pieces, then shuffle
        for (var i = 0; i < 7; i++)
            pieceBag.Add(i);

        // Fisher-Yates shuffle
        for (var i = pieceBag.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (pieceBag[i], pieceBag[j]) = (pieceBag[j], pieceBag[i]);
        }
    }

    private void ChooseBestPlacement(out int bestRotation, out int bestCol)
    {
        bestRotation = 0;
        bestCol = 0;
        var bestScore = float.NegativeInfinity;

        var rotationCount = Pieces[currentPiece].Length;

        for (var rot = 0; rot < rotationCount; rot++)
        {
            var cells = Pieces[currentPiece][rot];
            var minCx = int.MaxValue;
            var maxCx = int.MinValue;
            for (var i = 0; i < cells.Length; i += 2)
            {
                if (cells[i] < minCx) minCx = cells[i];
                if (cells[i] > maxCx) maxCx = cells[i];
            }

            for (var col = -minCx; col <= Cols - 1 - maxCx; col++)
            {
                // Find landing row
                var dropY = 0;
                while (CanPlace(currentPiece, rot, col, dropY + 1))
                    dropY++;

                if (!CanPlace(currentPiece, rot, col, dropY))
                    continue;

                var score = EvaluatePlacement(currentPiece, rot, col, dropY);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRotation = rot;
                    bestCol = col;
                }
            }
        }
    }

    private float EvaluatePlacement(int piece, int rotation, int col, int row)
    {
        // Temporarily place piece
        var cells = Pieces[piece][rotation];
        var colorIdx = piece + 1;

        for (var i = 0; i < cells.Length; i += 2)
            grid[col + cells[i], row + cells[i + 1]] = colorIdx;

        // Count complete lines
        var completeLines = 0;
        for (var y = 0; y < Rows; y++)
        {
            var full = true;
            for (var x = 0; x < Cols; x++)
            {
                if (grid[x, y] == 0)
                {
                    full = false;
                    break;
                }
            }
            if (full) completeLines++;
        }

        // Calculate aggregate height and holes
        var aggregateHeight = 0;
        var holes = 0;
        var bumpiness = 0;
        var columnHeights = new int[Cols];

        for (var x = 0; x < Cols; x++)
        {
            var colHeight = 0;
            for (var y = 0; y < Rows; y++)
            {
                if (grid[x, y] != 0)
                {
                    colHeight = Rows - y;
                    break;
                }
            }
            columnHeights[x] = colHeight;
            aggregateHeight += colHeight;

            // Count holes (empty cells below the highest filled cell)
            var foundFilled = false;
            for (var y = 0; y < Rows; y++)
            {
                if (grid[x, y] != 0)
                    foundFilled = true;
                else if (foundFilled)
                    holes++;
            }
        }

        for (var x = 0; x < Cols - 1; x++)
            bumpiness += Math.Abs(columnHeights[x] - columnHeights[x + 1]);

        // Remove piece
        for (var i = 0; i < cells.Length; i += 2)
            grid[col + cells[i], row + cells[i + 1]] = 0;

        // Weighted heuristic (standard Tetris AI weights)
        return completeLines * 7.6f
               - aggregateHeight * 0.51f
               - holes * 3.5f
               - bumpiness * 0.18f;
    }

    private bool CanPlace(int piece, int rotation, int col, int row)
    {
        var cells = Pieces[piece][rotation];
        for (var i = 0; i < cells.Length; i += 2)
        {
            var cx = col + cells[i];
            var cy = row + cells[i + 1];
            if (cx < 0 || cx >= Cols || cy < 0 || cy >= Rows)
                return false;
            if (grid[cx, cy] != 0)
                return false;
        }
        return true;
    }

    private void LockPiece()
    {
        var cells = Pieces[currentPiece][currentRotation];
        var colorIdx = currentPiece + 1;
        for (var i = 0; i < cells.Length; i += 2)
            grid[currentX + cells[i], currentY + cells[i + 1]] = colorIdx;

        hasCurrent = false;
    }

    private void CheckLines()
    {
        flashingRows.Clear();
        for (var y = 0; y < Rows; y++)
        {
            var full = true;
            for (var x = 0; x < Cols; x++)
            {
                if (grid[x, y] == 0)
                {
                    full = false;
                    break;
                }
            }
            if (full)
                flashingRows.Add(y);
        }

        if (flashingRows.Count > 0)
        {
            flashTimer = FlashDuration;
            linesCleared += flashingRows.Count;
        }
    }

    private void CollapseFlashingRows()
    {
        // Process rows from bottom to top so shifting doesn't corrupt indices
        var sortedRows = new List<int>(flashingRows);
        sortedRows.Sort();
        sortedRows.Reverse();

        foreach (var clearedRow in sortedRows)
        {
            // Shift everything above down by one
            for (var y = clearedRow; y > 0; y--)
            for (var x = 0; x < Cols; x++)
                grid[x, y] = grid[x, y - 1];

            // Clear top row
            for (var x = 0; x < Cols; x++)
                grid[x, 0] = 0;
        }
    }

    private void DrawBackground(Image<Rgba32> img)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            img[x, y] = new Rgba32(8, 8, 12);
    }

    private static void DrawBorder(Image<Rgba32> img)
    {
        var borderColor = new Rgba32(60, 60, 80);
        var bx = OffsetX - 1;
        var by = OffsetY - 1;
        var bw = WellPixelW + 2;
        var bh = Rows * CellH + 2;

        // Top and bottom
        for (var x = 0; x < bw; x++)
        {
            SetPixel(img, bx + x, by, borderColor);
            SetPixel(img, bx + x, by + bh - 1, borderColor);
        }

        // Left and right
        for (var y = 0; y < bh; y++)
        {
            SetPixel(img, bx, by + y, borderColor);
            SetPixel(img, bx + bw - 1, by + y, borderColor);
        }
    }

    private void DrawGrid(Image<Rgba32> img)
    {
        for (var y = 0; y < Rows; y++)
        for (var x = 0; x < Cols; x++)
        {
            var cell = grid[x, y];
            if (cell == 0) continue;

            if (flashingRows.Contains(y))
            {
                var flash = flashTimer / FlashDuration;
                var intensity = ((int)(flash * 6f) & 1) == 0;
                var color = intensity
                    ? new Rgba32(255, 255, 255)
                    : DimColor(PieceColors[cell - 1], 0.4f);
                DrawCell(img, x, y, color);
            }
            else
            {
                DrawCell(img, x, y, PieceColors[cell - 1]);
            }
        }
    }

    private static void DrawPiece(Image<Rgba32> img, int piece, int rotation, int col, int row, Rgba32 color)
    {
        var cells = Pieces[piece][rotation];
        for (var i = 0; i < cells.Length; i += 2)
            DrawCell(img, col + cells[i], row + cells[i + 1], color);
    }

    private static void DrawCell(Image<Rgba32> img, int gridX, int gridY, Rgba32 color)
    {
        var px = OffsetX + gridX * CellW;
        var py = OffsetY + gridY * CellH;
        for (var dy = 0; dy < CellH; dy++)
        for (var dx = 0; dx < CellW; dx++)
            SetPixel(img, px + dx, py + dy, color);
    }

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return;

        img[x, y] = color;
    }

    private static Rgba32 DimColor(Rgba32 color, float factor)
    {
        return new Rgba32(
            (byte)(color.R * factor),
            (byte)(color.G * factor),
            (byte)(color.B * factor));
    }
}
