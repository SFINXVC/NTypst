using System.Runtime.InteropServices;

namespace NTypst.Native;

internal sealed unsafe class NativeCompileResult : IDisposable
{
    private TypstCompileResult* _handle;
    private bool _disposed;

    internal NativeCompileResult(TypstCompileResult* handle)
    {
        _handle = handle;
    }

    internal bool IsSuccess => NativeMethods.typst_compile_result_is_success(_handle);

    internal NativePagedDocumentHandle? TakePagedDocument()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var doc = NativeMethods.typst_compile_result_take_paged_document(_handle);
        return doc == null ? null : new NativePagedDocumentHandle(doc);
    }

    internal NativeHtmlDocumentHandle? TakeHtmlDocument()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var doc = NativeMethods.typst_compile_result_take_html_document(_handle);
        return doc == null ? null : new NativeHtmlDocumentHandle(doc);
    }

    internal int WarningCount => (int)NativeMethods.typst_compile_result_warning_count(_handle);
    internal int ErrorCount => (int)NativeMethods.typst_compile_result_error_count(_handle);

    internal string? GetDiagnosticMessage(int kind, int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var ptr = NativeMethods.typst_compile_result_diagnostic_message(_handle, kind, (nuint)index);
        return ptr == null ? null : Marshal.PtrToStringUTF8((nint)ptr);
    }

    internal int GetDiagnosticSeverity(int kind, int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return NativeMethods.typst_compile_result_diagnostic_severity(_handle, kind, (nuint)index);
    }

    internal int GetDiagnosticHintCount(int kind, int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return (int)NativeMethods.typst_compile_result_diagnostic_hint_count(_handle, kind, (nuint)index);
    }

    internal string? GetDiagnosticHint(int kind, int index, int hintIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var ptr = NativeMethods.typst_compile_result_diagnostic_hint(_handle, kind, (nuint)index, (nuint)hintIndex);
        return ptr == null ? null : Marshal.PtrToStringUTF8((nint)ptr);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != null)
        {
            NativeMethods.typst_compile_result_free(_handle);
            _handle = null;
        }
    }
}
