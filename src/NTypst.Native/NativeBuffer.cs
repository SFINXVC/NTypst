using System.Runtime.InteropServices;

namespace NTypst.Native;

internal sealed unsafe class NativeBuffer : IDisposable
{
    private TypstBuffer* _handle;
    private bool _disposed;

    internal NativeBuffer(TypstBuffer* handle)
    {
        _handle = handle;
    }

    internal byte[] ToArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var data = NativeMethods.typst_buffer_data(_handle);
        var len = (int)NativeMethods.typst_buffer_len(_handle);
        var result = new byte[len];
        new ReadOnlySpan<byte>(data, len).CopyTo(result);
        return result;
    }

    internal string ToStringUtf8()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var data = NativeMethods.typst_buffer_data(_handle);
        var len = (int)NativeMethods.typst_buffer_len(_handle);
        return Marshal.PtrToStringUTF8((nint)data, len) ?? string.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != null)
        {
            NativeMethods.typst_buffer_free(_handle);
            _handle = null;
        }
    }
}
