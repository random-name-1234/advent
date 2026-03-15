using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class ErrorScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;

    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(7.8);

    private static readonly IReadOnlyDictionary<char, string[]> SmallGlyphs = new Dictionary<char, string[]>
    {
        [' '] =
        [
            "000",
            "000",
            "000",
            "000",
            "000"
        ],
        ['1'] =
        [
            "010",
            "110",
            "010",
            "010",
            "111"
        ],
        ['3'] =
        [
            "111",
            "001",
            "011",
            "001",
            "111"
        ],
        ['C'] =
        [
            "111",
            "100",
            "100",
            "100",
            "111"
        ],
        ['D'] =
        [
            "110",
            "101",
            "101",
            "101",
            "110"
        ],
        ['E'] =
        [
            "111",
            "100",
            "110",
            "100",
            "111"
        ],
        ['K'] =
        [
            "101",
            "101",
            "110",
            "101",
            "101"
        ],
        ['O'] =
        [
            "111",
            "101",
            "101",
            "101",
            "111"
        ],
        ['R'] =
        [
            "110",
            "101",
            "110",
            "101",
            "101"
        ]
    };

    private TimeSpan elapsedThisScene;
    private int noiseSeed;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Error";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        noiseSeed = Random.Shared.Next(1, int.MaxValue);
        IsActive = true;
        HidesTime = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive) return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene >= SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        var t = (float)elapsedThisScene.TotalSeconds;
        var fade = t > 7.0f ? Math.Clamp((7.8f - t) / 0.8f, 0f, 1f) : 1f;

        DrawBackdrop(img, t, fade);

        if (t < 2.6f)
        {
            DrawAlarmFrame(img, t, fade);
        }
        else if (t < 5.2f)
        {
            DrawGlitchFrame(img, t, fade);
        }
        else
        {
            DrawRecoveryFrame(img, t, fade);
        }
    }

    private void DrawBackdrop(Image<Rgba32> img, float time, float fade)
    {
        for (var y = 0; y < Height; y++)
        {
            var depth = y / (float)(Height - 1);
            var baseColor = new Rgba32(
                ToByte(7f + depth * 11f),
                ToByte(5f + depth * 3f),
                ToByte(14f + depth * 10f));

            var scan = ((y + (int)(time * 24f)) & 3) == 0 ? 0.68f : 1f;
            var rowColor = Scale(baseColor, fade * scan);
            for (var x = 0; x < Width; x++) img[x, y] = rowColor;
        }
    }

    private void DrawAlarmFrame(Image<Rgba32> img, float time, float fade)
    {
        var border = ((int)(time * 8f) & 1) == 0 ? new Rgba32(255, 76, 64) : new Rgba32(255, 156, 76);
        DrawBorder(img, Scale(border, fade));
        DrawWarningTriangle(img, 24, 7, 16, fade);
        DrawExclamation(img, 31, 14, Scale(new Rgba32(255, 242, 210), fade));
        DrawTextSmall(img, 6, 3, "ERROR", Scale(new Rgba32(255, 206, 116), fade));

        for (var i = 0; i < 9; i++)
        {
            var pulse = 0.45f + 0.55f * MathF.Sin(time * 4.6f + i * 0.75f);
            var barHeight = 1 + (int)MathF.Round(pulse * 4f);
            var x = 6 + i * 6;
            FillRect(img, x, 30 - barHeight, 4, barHeight, Scale(new Rgba32(242, 82, 68), fade));
        }
    }

    private void DrawGlitchFrame(Image<Rgba32> img, float time, float fade)
    {
        DrawBorder(img, Scale(new Rgba32(196, 76, 255), fade));
        DrawTextSmall(img, 16, 3, "CODE13", Scale(new Rgba32(222, 196, 255), fade));

        var bucket = (int)(time * 26f);
        for (var y = 6; y < Height - 2; y++)
        {
            var rowOffset = ((Hash(bucket, y, noiseSeed) & 7) - 3);
            for (var x = 2; x < Width - 2; x++)
            {
                var h = Hash(x + rowOffset, y, bucket + noiseSeed);
                if ((h & 127) == 0)
                {
                    var c = Scale(new Rgba32(255, 120, 210), fade);
                    SetPixel(img, x, y, c);
                    SetPixel(img, x + 1, y, c);
                }
                else if ((h & 255) == 3)
                {
                    FillRect(img, x, y, 2, 1, Scale(new Rgba32(82, 190, 255), fade));
                }
            }
        }

        DrawCrossIcon(img, 24, 15, Scale(new Rgba32(255, 186, 164), fade));
        DrawCrossIcon(img, 39, 15, Scale(new Rgba32(255, 186, 164), fade));
        FillRect(img, 28, 22, 9, 1, Scale(new Rgba32(240, 94, 112), fade));
    }

    private void DrawRecoveryFrame(Image<Rgba32> img, float time, float fade)
    {
        DrawBorder(img, Scale(new Rgba32(94, 230, 146), fade));
        DrawTextSmall(img, 27, 6, "OK", Scale(new Rgba32(188, 255, 211), fade));

        var progress = Math.Clamp((time - 5.2f) / 2.4f, 0f, 1f);
        FillRect(img, 9, 22, 46, 6, Scale(new Rgba32(20, 42, 32), fade));
        FillRect(img, 10, 23, 44, 4, Scale(new Rgba32(32, 58, 43), fade));
        FillRect(img, 10, 23, (int)MathF.Round(44f * progress), 4, Scale(new Rgba32(78, 220, 124), fade));

        DrawCheck(img, 22, 14, Scale(new Rgba32(208, 255, 176), fade));
        var pulse = 0.55f + 0.45f * MathF.Sin(time * 7.2f);
        FillRect(img, 49, 9, 4, 4, Scale(new Rgba32(86, 220, 240), fade * pulse));
    }

    private static void DrawBorder(Image<Rgba32> img, Rgba32 color)
    {
        for (var x = 0; x < Width; x++)
        {
            SetPixel(img, x, 0, color);
            SetPixel(img, x, Height - 1, color);
        }

        for (var y = 0; y < Height; y++)
        {
            SetPixel(img, 0, y, color);
            SetPixel(img, Width - 1, y, color);
        }
    }

    private static void DrawWarningTriangle(Image<Rgba32> img, int x, int y, int size, float fade)
    {
        var fill = Scale(new Rgba32(114, 32, 28), fade);
        var edge = Scale(new Rgba32(255, 178, 104), fade);
        var half = size / 2;

        for (var row = 0; row < size; row++)
        {
            var span = Math.Max(1, row);
            var left = x + half - span / 2;
            var right = x + half + span / 2;
            for (var px = left; px <= right; px++) SetPixel(img, px, y + row, fill);
            SetPixel(img, left, y + row, edge);
            SetPixel(img, right, y + row, edge);
        }

        for (var px = x + half - size / 2; px <= x + half + size / 2; px++) SetPixel(img, px, y + size - 1, edge);
    }

    private static void DrawExclamation(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        FillRect(img, x, y, 2, 5, color);
        FillRect(img, x, y + 7, 2, 2, color);
    }

    private static void DrawCheck(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        SetPixel(img, x, y + 3, color);
        SetPixel(img, x + 1, y + 4, color);
        SetPixel(img, x + 2, y + 5, color);
        SetPixel(img, x + 3, y + 4, color);
        SetPixel(img, x + 4, y + 3, color);
        SetPixel(img, x + 5, y + 2, color);
        SetPixel(img, x + 6, y + 1, color);
    }

    private static void DrawCrossIcon(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        SetPixel(img, x, y, color);
        SetPixel(img, x + 1, y + 1, color);
        SetPixel(img, x + 2, y + 2, color);
        SetPixel(img, x + 2, y, color);
        SetPixel(img, x + 1, y + 1, color);
        SetPixel(img, x, y + 2, color);
    }

    private static void DrawTextSmall(Image<Rgba32> img, int x, int y, string text, Rgba32 color)
    {
        var cursor = x;
        foreach (var rawChar in text)
        {
            var c = char.ToUpperInvariant(rawChar);
            if (!SmallGlyphs.TryGetValue(c, out var glyph))
            {
                cursor += 4;
                continue;
            }

            for (var row = 0; row < glyph.Length; row++)
            {
                var bits = glyph[row];
                for (var col = 0; col < bits.Length; col++)
                {
                    if (bits[col] == '1') SetPixel(img, cursor + col, y + row, color);
                }
            }

            cursor += 4;
        }
    }

    private static void FillRect(Image<Rgba32> img, int x, int y, int width, int height, Rgba32 color)
    {
        for (var py = y; py < y + height; py++)
        for (var px = x; px < x + width; px++)
            SetPixel(img, px, py, color);
    }

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x >= Width || (uint)y >= Height) return;
        img[x, y] = color;
    }

    private static int Hash(int x, int y, int z)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + z * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    private static Rgba32 Scale(Rgba32 color, float factor)
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        return new Rgba32(
            ToByte(color.R * clamped),
            ToByte(color.G * clamped),
            ToByte(color.B * clamped));
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }
}
