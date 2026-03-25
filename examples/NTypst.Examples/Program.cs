using System.Text;
using NTypst;

var rootDir = Path.GetFullPath(args.Length > 0 ? args[0] : ".");

byte[]? ResolveFile(string path)
{
    var fullPath = Path.GetFullPath(Path.Combine(rootDir, path.TrimStart('/')));
    if (!fullPath.StartsWith(rootDir, StringComparison.Ordinal))
        return null;

    return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
}

using var world = new TypstWorld(sourceResolver: ResolveFile, fileResolver: ResolveFile);
world.AddSystemFonts();
world.SetMain("/main.typ");

try
{
    var result = world.CompilePaged();
    using var document = result.Document;

    Console.WriteLine($"Compiled: {document.PageCount} page(s)");

    foreach (var warning in result.Warnings)
        Console.WriteLine($"  Warning: {warning.Message}");

    File.WriteAllBytes(Path.Combine(rootDir, "output.pdf"), document.ExportPdf());

    for (var i = 0; i < document.PageCount; i++)
        File.WriteAllBytes(Path.Combine(rootDir, $"page-{i}.png"), document.ExportPng(i, pixelsPerPoint: 3.0f));

    File.WriteAllText(Path.Combine(rootDir, "output.svg"), document.ExportSvg(), Encoding.UTF8);
}
catch (TypstCompilationException ex)
{
    Console.Error.WriteLine(ex.Message);

    foreach (var diag in ex.Diagnostics)
    {
        Console.Error.WriteLine($"  {diag.Severity}: {diag.Message}");
        foreach (var hint in diag.Hints)
            Console.Error.WriteLine($"    Hint: {hint}");
    }
}
