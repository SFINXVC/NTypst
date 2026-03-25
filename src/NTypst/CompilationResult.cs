namespace NTypst;

/// <summary>
/// The result of a successful Typst compilation, containing the document and any warnings.
/// </summary>
/// <typeparam name="TDocument">The type of document produced.</typeparam>
public sealed class CompilationResult<TDocument> where TDocument : IDisposable
{
    /// <summary>
    /// The compiled document.
    /// </summary>
    public TDocument Document { get; }

    /// <summary>
    /// Warnings emitted during compilation. May be empty.
    /// </summary>
    public IReadOnlyList<TypstDiagnostic> Warnings { get; }

    internal CompilationResult(TDocument document, IReadOnlyList<TypstDiagnostic> warnings)
    {
        Document = document;
        Warnings = warnings;
    }
}
