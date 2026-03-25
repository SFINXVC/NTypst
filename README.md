# NTypst


[<img alt="github" src="https://img.shields.io/badge/github-sfinxvc/typst--sharp-8da0cb?style=for-the-badge&labelColor=555555&logo=github" height="20">](https://github.com/sfinxvc/ntypst)
[<img alt="nuget" src="https://img.shields.io/nuget/v/NTypst.svg?style=for-the-badge&color=fc8d62&logo=nuget" height="20">](https://www.nuget.org/packages/NTypst)
[<img alt="build status" src="https://img.shields.io/github/actions/workflow/status/sfinxvc/ntypst/dotnet.yml?branch=main&style=for-the-badge" height="20">](https://github.com/sfinxvc/ntypst/actions?query=branch%3Amain)


A C# wrapper for the [Typst](https://typst.app) compiler. Compile Typst markup to PDF, PNG, SVG, and HTML from .NET.

## Usage

```csharp
using NTypst;

byte[]? ResolveFile(string path)
{
    var fullPath = Path.GetFullPath(Path.Combine(".", path.TrimStart('/')));
    return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
}

using var world = new TypstWorld(sourceResolver: ResolveFile, fileResolver: ResolveFile);
world.AddSystemFonts();
world.SetMain("/main.typ");

var result = world.CompilePaged();
using var document = result.Document;

File.WriteAllBytes("output.pdf", document.ExportPdf());
```

### Inline source

```csharp
using System.Text;
using NTypst;

var source = "= Hello from C#\nThis is *Typst*.";

using var world = new TypstWorld(
    sourceResolver: path => path == "/main.typ" ? Encoding.UTF8.GetBytes(source) : null,
    fileResolver: _ => null);

world.AddSystemFonts();
world.SetMain("/main.typ");

var result = world.CompilePaged();
using var document = result.Document;

File.WriteAllBytes("output.pdf", document.ExportPdf());
```

### Export formats

```csharp
// PDF
byte[] pdf = document.ExportPdf();

// PNG (per page)
byte[] png = document.ExportPng(pageIndex: 0, pixelsPerPoint: 3.0f);

// SVG (all pages merged)
string svg = document.ExportSvg();

// SVG (single page)
string svgPage = document.ExportSvg(pageIndex: 0);
```

### HTML compilation

```csharp
var result = world.CompileHtml();
using var htmlDoc = result.Document;
string html = htmlDoc.Export();
```

### Error handling

```csharp
try
{
    var result = world.CompilePaged();
}
catch (TypstCompilationException ex)
{
    foreach (var diag in ex.Diagnostics)
    {
        Console.Error.WriteLine($"{diag.Severity}: {diag.Message}");
        foreach (var hint in diag.Hints)
            Console.Error.WriteLine($"  Hint: {hint}");
    }
}
```

### Warnings

Successful compilations may still produce warnings:

```csharp
var result = world.CompilePaged();

foreach (var warning in result.Warnings)
    Console.WriteLine($"Warning: {warning.Message}");
```

## Building from source

Requires [Rust](https://rustup.rs) and [.NET 10 SDK](https://dotnet.microsoft.com).

```sh
cd native && cargo build
cd .. && dotnet build
```

To run the example:

```sh
LD_LIBRARY_PATH=native/target/debug dotnet run --project examples/NTypst.Examples
```

## License

MIT