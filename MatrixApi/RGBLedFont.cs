using System;
using System.Runtime.InteropServices;

namespace MatrixApi;

public class RGBLedFont : IDisposable
{
    internal IntPtr _font;

    public RGBLedFont(string bdf_font_file_path)
    {
        _font = load_font(bdf_font_file_path);
    }

    [DllImport("librgbmatrix.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr load_font(string bdf_font_file);

    [DllImport("librgbmatrix.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int draw_text(IntPtr canvas, IntPtr font, int x, int y, byte r, byte g, byte b,
        string utf8_text, int extra_spacing);

    [DllImport("librgbmatrix.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int vertical_draw_text(IntPtr canvas, IntPtr font, int x, int y, byte r, byte g, byte b,
        string utf8_text, int kerning_offset);

    [DllImport("librgbmatrix.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern void delete_font(IntPtr font);

    internal int DrawText(IntPtr canvas, int x, int y, Color color, string text, int spacing = 0, bool vertical = false)
    {
        if (!vertical)
            return draw_text(canvas, _font, x, y, color.R, color.G, color.B, text, spacing);
        return vertical_draw_text(canvas, _font, x, y, color.R, color.G, color.B, text, spacing);
    }

    #region IDisposable Support

    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            delete_font(_font);
            disposedValue = true;
        }
    }

    ~RGBLedFont()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}