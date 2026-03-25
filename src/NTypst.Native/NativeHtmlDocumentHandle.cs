namespace NTypst.Native;

internal sealed unsafe class NativeHtmlDocumentHandle : IDisposable
{
    private TypstHtmlDocument* _handle;
    private bool _disposed;

    internal NativeHtmlDocumentHandle(TypstHtmlDocument* handle)
    {
        _handle = handle;
    }

    internal NativeBuffer? Export()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var buf = NativeMethods.typst_html_document_export(_handle);
        return buf == null ? null : new NativeBuffer(buf);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != null)
        {
            NativeMethods.typst_html_document_free(_handle);
            _handle = null;
        }
    }
}
