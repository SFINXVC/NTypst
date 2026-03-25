using NTypst.Native;

namespace NTypst;

/// <summary>
/// A compiled HTML Typst document.
/// </summary>
public sealed class HtmlDocument : IDisposable
{
    private readonly NativeHtmlDocumentHandle _handle;
    private bool _disposed;

    internal HtmlDocument(NativeHtmlDocumentHandle handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Export the document to an HTML string.
    /// </summary>
    /// <returns>The HTML content as a string.</returns>
    public string Export()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var buf = _handle.Export()
            ?? throw new InvalidOperationException("HTML export failed.");
        return buf.ToStringUtf8();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle.Dispose();
    }
}
