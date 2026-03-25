using NTypst.Native;

namespace NTypst;

/// <summary>
/// A compiled paged Typst document, suitable for PDF, PNG, and SVG export.
/// </summary>
public sealed class PagedDocument : IDisposable
{
    private readonly NativePagedDocumentHandle _handle;
    private bool _disposed;

    internal PagedDocument(NativePagedDocumentHandle handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// The number of pages in this document.
    /// </summary>
    public int PageCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle.PageCount;
        }
    }

    /// <summary>
    /// Export the document to PDF.
    /// </summary>
    /// <returns>The PDF file contents as a byte array.</returns>
    public byte[] ExportPdf()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var buf = _handle.ExportPdf()
            ?? throw new InvalidOperationException("PDF export failed.");
        return buf.ToArray();
    }

    /// <summary>
    /// Export a single page to PNG.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="pixelsPerPoint">Rendering resolution (e.g., 3.0 for high-DPI).</param>
    /// <returns>The PNG file contents as a byte array.</returns>
    public byte[] ExportPng(int pageIndex, float pixelsPerPoint = 2.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pixelsPerPoint, 0);

        using var buf = _handle.ExportPng(pageIndex, pixelsPerPoint)
            ?? throw new InvalidOperationException($"PNG export failed for page {pageIndex}.");
        return buf.ToArray();
    }

    /// <summary>
    /// Export all pages as a single merged SVG.
    /// </summary>
    /// <returns>The SVG content as a string.</returns>
    public string ExportSvg()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var buf = _handle.ExportSvg()
            ?? throw new InvalidOperationException("SVG export failed.");
        return buf.ToStringUtf8();
    }

    /// <summary>
    /// Export a single page to SVG.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <returns>The SVG content as a string.</returns>
    public string ExportSvg(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);

        using var buf = _handle.ExportSvgPage(pageIndex)
            ?? throw new InvalidOperationException($"SVG export failed for page {pageIndex}.");
        return buf.ToStringUtf8();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle.Dispose();
    }
}
