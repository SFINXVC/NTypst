namespace NTypst;

/// <summary>
/// A single diagnostic emitted during Typst compilation.
/// </summary>
public sealed class TypstDiagnostic
{
    /// <summary>
    /// The severity of this diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// The human-readable diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Hints that may help resolve the diagnostic.
    /// </summary>
    public IReadOnlyList<string> Hints { get; }

    internal TypstDiagnostic(DiagnosticSeverity severity, string message, IReadOnlyList<string> hints)
    {
        Severity = severity;
        Message = message;
        Hints = hints;
    }

    public override string ToString() => $"{Severity}: {Message}";
}
