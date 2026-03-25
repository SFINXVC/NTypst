using NTypst.Native;

namespace NTypst;

/// <summary>
/// The Typst compilation world. Manages source resolution, fonts, and compilation.
/// </summary>
public sealed class TypstWorld : IDisposable
{
    private readonly NativeWorldHandle _handle;
    private bool _disposed;

    /// <summary>
    /// Creates a new Typst world with the given source and file resolvers.
    /// </summary>
    /// <param name="sourceResolver">
    /// Resolves Typst source files by virtual path.
    /// Return the file contents as a byte array, or <c>null</c> if not found.
    /// </param>
    /// <param name="fileResolver">
    /// Resolves non-source files (images, data) by virtual path.
    /// Return the file contents as a byte array, or <c>null</c> if not found.
    /// </param>
    public TypstWorld(Func<string, byte[]?> sourceResolver, Func<string, byte[]?> fileResolver)
    {
        ArgumentNullException.ThrowIfNull(sourceResolver);
        ArgumentNullException.ThrowIfNull(fileResolver);
        _handle = new NativeWorldHandle(sourceResolver, fileResolver);
    }

    /// <summary>
    /// Sets the main source file path for compilation.
    /// </summary>
    /// <param name="path">The virtual path to the main source file (e.g., "/main.typ").</param>
    public void SetMain(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(path);
        _handle.SetMain(path);
    }

    /// <summary>
    /// Adds font(s) from raw font file data. Supports .ttf, .otf, and .ttc files.
    /// </summary>
    /// <param name="data">The raw font file bytes.</param>
    /// <returns>The number of fonts loaded from the data.</returns>
    public int AddFont(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _handle.AddFont(data);
    }

    /// <summary>
    /// Discovers and adds all system-installed fonts.
    /// </summary>
    /// <returns>The number of fonts discovered.</returns>
    public int AddSystemFonts()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _handle.AddSystemFonts();
    }

    /// <summary>
    /// Compiles the main source into a paged document (for PDF, PNG, SVG export).
    /// </summary>
    /// <returns>A compilation result containing the document and any warnings.</returns>
    /// <exception cref="TypstCompilationException">Thrown when compilation fails with errors.</exception>
    public CompilationResult<PagedDocument> CompilePaged()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Compile(isHtml: false, result =>
        {
            var doc = result.TakePagedDocument()
                ?? throw new InvalidOperationException("Failed to extract paged document from successful compilation.");
            return new PagedDocument(doc);
        });
    }

    /// <summary>
    /// Compiles the main source into an HTML document.
    /// </summary>
    /// <returns>A compilation result containing the document and any warnings.</returns>
    /// <exception cref="TypstCompilationException">Thrown when compilation fails with errors.</exception>
    public CompilationResult<HtmlDocument> CompileHtml()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Compile(isHtml: true, result =>
        {
            var doc = result.TakeHtmlDocument()
                ?? throw new InvalidOperationException("Failed to extract HTML document from successful compilation.");
            return new HtmlDocument(doc);
        });
    }

    private CompilationResult<TDocument> Compile<TDocument>(
        bool isHtml,
        Func<NativeCompileResult, TDocument> extractDocument)
        where TDocument : IDisposable
    {
        _handle.SetResolvers();
        try
        {
            using var result = isHtml
                ? CompileResultInterop.CompileHtml(_handle)
                : CompileResultInterop.CompilePaged(_handle);

            if (!result.IsSuccess)
            {
                var errors = ExtractDiagnostics(result, kind: 1);
                throw new TypstCompilationException(errors);
            }

            var warnings = ExtractDiagnostics(result, kind: 0);
            var document = extractDocument(result);
            return new CompilationResult<TDocument>(document, warnings);
        }
        finally
        {
            _handle.ClearResolvers();
        }
    }

    private static List<TypstDiagnostic> ExtractDiagnostics(NativeCompileResult result, int kind)
    {
        var count = kind == 0 ? result.WarningCount : result.ErrorCount;
        var diagnostics = new List<TypstDiagnostic>(count);

        for (var i = 0; i < count; i++)
        {
            var severity = (DiagnosticSeverity)result.GetDiagnosticSeverity(kind, i);
            var message = result.GetDiagnosticMessage(kind, i) ?? string.Empty;

            var hintCount = result.GetDiagnosticHintCount(kind, i);
            var hints = new List<string>(hintCount);
            for (var h = 0; h < hintCount; h++)
            {
                var hint = result.GetDiagnosticHint(kind, i, h);
                if (hint is not null)
                    hints.Add(hint);
            }

            diagnostics.Add(new TypstDiagnostic(severity, message, hints));
        }

        return diagnostics;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle.Dispose();
    }
}
