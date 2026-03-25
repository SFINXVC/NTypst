namespace NTypst.Native;

internal sealed unsafe class NativePagedDocumentHandle : IDisposable
{
    private TypstPagedDocument* _handle;
    private bool _disposed;

    internal NativePagedDocumentHandle(TypstPagedDocument* handle)
    {
        _handle = handle;
    }

    internal int PageCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return (int)NativeMethods.typst_paged_document_page_count(_handle);
        }
    }

    internal NativeBuffer? ExportPdf()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var buf = NativeMethods.typst_paged_document_export_pdf(_handle);
        return buf == null ? null : new NativeBuffer(buf);
    }

    internal NativeBuffer? ExportPng(int pageIndex, float pixelPerPt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var buf = NativeMethods.typst_paged_document_export_png(_handle, (nuint)pageIndex, pixelPerPt);
        return buf == null ? null : new NativeBuffer(buf);
    }

    internal NativeBuffer? ExportSvg()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var buf = NativeMethods.typst_paged_document_export_svg(_handle);
        return buf == null ? null : new NativeBuffer(buf);
    }

    internal NativeBuffer? ExportSvgPage(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var buf = NativeMethods.typst_paged_document_export_svg_page(_handle, (nuint)pageIndex);
        return buf == null ? null : new NativeBuffer(buf);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != null)
        {
            NativeMethods.typst_paged_document_free(_handle);
            _handle = null;
        }
    }
}
