using System;
using static MatrixApi.Bindings;

namespace MatrixApi;

public class RGBLedCanvas
{
    internal IntPtr _canvas;

    internal RGBLedCanvas(IntPtr canvas)
    {
        _canvas = canvas;
        led_canvas_get_size(_canvas, out var width, out var height);
        Width = width;
        Height = height;
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public IntPtr Handle => _canvas;

    public void SetPixel(int x, int y, Color color) => led_canvas_set_pixel(_canvas, x, y, color.R, color.G, color.B);

    public void SetPixels(int x, int y, int width, int height, Span<Color> colors)
    {
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));

        var required = width * height;
        if (colors.Length < required)
            throw new ArgumentOutOfRangeException(nameof(colors));
        if (required == 0) return;

        led_canvas_set_pixels(_canvas, x, y, width, height, ref colors[0]);
    }

    public void Fill(Color color) => led_canvas_fill(_canvas, color.R, color.G, color.B);

    public void SubFill(int x, int y, int width, int height, Color color) =>
        led_canvas_subfill(_canvas, x, y, width, height, color.R, color.G, color.B);

    public void Clear() => led_canvas_clear(_canvas);

    public void DrawCircle(int x, int y, int radius, Color color) =>
        draw_circle(_canvas, x, y, radius, color.R, color.G, color.B);

    public void DrawLine(int x0, int y0, int x1, int y1, Color color) =>
        draw_line(_canvas, x0, y0, x1, y1, color.R, color.G, color.B);

    public int DrawText(RGBLedFont font, int x, int y, Color color, string text, int spacing = 0, bool vertical = false) =>
        font.DrawText(_canvas, x, y, color, text, spacing, vertical);
}
