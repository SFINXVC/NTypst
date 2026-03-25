namespace NTypst;

/// <summary>
/// Exception thrown when Typst compilation fails with errors.
/// </summary>
public class TypstCompilationException : InvalidOperationException
{
    /// <summary>
    /// The error diagnostics that caused the compilation to fail.
    /// </summary>
    public IReadOnlyList<TypstDiagnostic> Diagnostics { get; }

    internal TypstCompilationException(IReadOnlyList<TypstDiagnostic> diagnostics)
        : base(FormatMessage(diagnostics))
    {
        Diagnostics = diagnostics;
    }

    private static string FormatMessage(IReadOnlyList<TypstDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
            return "Typst compilation failed.";

        if (diagnostics.Count == 1)
            return $"Typst compilation failed: {diagnostics[0].Message}";

        return $"Typst compilation failed with {diagnostics.Count} errors. First: {diagnostics[0].Message}";
    }
}
