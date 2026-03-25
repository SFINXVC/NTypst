namespace NTypst;

/// <summary>
/// Severity of a Typst compilation diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>A non-fatal warning.</summary>
    Warning = 0,

    /// <summary>A fatal compilation error.</summary>
    Error = 1,
}
