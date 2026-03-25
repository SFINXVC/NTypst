namespace NTypst.Native;

internal static unsafe class CompileResultInterop
{
    internal static NativeCompileResult CompilePaged(NativeWorldHandle world)
    {
        var result = NativeMethods.typst_compile_paged(world.Handle);
        return new NativeCompileResult(result);
    }

    internal static NativeCompileResult CompileHtml(NativeWorldHandle world)
    {
        var result = NativeMethods.typst_compile_html(world.Handle);
        return new NativeCompileResult(result);
    }
}
