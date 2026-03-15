using System;
using static MatrixApi.Bindings;

namespace MatrixApi;

public sealed class ContentStreamer : IDisposable
{
    private IntPtr streamIo;
    private IntPtr reader;
    private bool disposed;

    public ContentStreamer(string filename)
    {
        streamIo = file_stream_io_create(filename);
        if (streamIo == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to open stream file: {filename}");

        reader = content_stream_reader_create(streamIo);
        if (reader == IntPtr.Zero)
        {
            file_stream_io_delete(streamIo);
            streamIo = IntPtr.Zero;
            throw new InvalidOperationException("Failed to create stream reader.");
        }
    }

    public void Rewind()
    {
        EnsureNotDisposed();
        content_stream_reader_rewind(reader);
    }

    public bool IsCompatible(RGBLedCanvas canvas)
    {
        if (canvas is null) throw new ArgumentNullException(nameof(canvas));
        return IsCompatible(canvas.Handle);
    }

    public bool IsCompatible(IntPtr frameCanvas)
    {
        EnsureNotDisposed();
        if (frameCanvas == IntPtr.Zero)
            throw new ArgumentException("Canvas handle must not be zero.", nameof(frameCanvas));

        return file_stream_io_is_compatible_with_canvas(streamIo, frameCanvas);
    }

    public bool GetNext(RGBLedCanvas canvas, out uint holdTimeUs)
    {
        if (canvas is null) throw new ArgumentNullException(nameof(canvas));
        return GetNext(canvas.Handle, out holdTimeUs);
    }

    public bool GetNext(IntPtr frameCanvas, out uint holdTimeUs)
    {
        EnsureNotDisposed();
        if (frameCanvas == IntPtr.Zero)
            throw new ArgumentException("Canvas handle must not be zero.", nameof(frameCanvas));

        return content_stream_reader_get_next(reader, frameCanvas, out holdTimeUs);
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed) return;

        if (reader != IntPtr.Zero)
        {
            content_stream_reader_destroy(reader);
            reader = IntPtr.Zero;
        }

        if (streamIo != IntPtr.Zero)
        {
            file_stream_io_delete(streamIo);
            streamIo = IntPtr.Zero;
        }

        disposed = true;
    }
}
