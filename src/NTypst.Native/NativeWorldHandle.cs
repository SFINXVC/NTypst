using System.Runtime.InteropServices;
using System.Text;

namespace NTypst.Native;

internal sealed unsafe class NativeWorldHandle : IDisposable
{
    private TypstWorldHandle* _handle;
    private bool _disposed;

    private readonly GCHandle _resolveSourcePin;
    private readonly GCHandle _resolveFilePin;
    private readonly GCHandle _freePin;

    private readonly Func<string, byte[]?> _sourceResolver;
    private readonly Func<string, byte[]?> _fileResolver;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int ResolveCallback(byte* pathPtr, byte** outBuf, nuint* outLen)
    {
        return -2;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void FreeCallback(byte* ptr, nuint len)
    {
        Marshal.FreeHGlobal((nint)ptr);
    }

    internal NativeWorldHandle(Func<string, byte[]?> sourceResolver, Func<string, byte[]?> fileResolver)
    {
        _sourceResolver = sourceResolver;
        _fileResolver = fileResolver;

        _resolveSourcePin = GCHandle.Alloc(_sourceResolver);
        _resolveFilePin = GCHandle.Alloc(_fileResolver);
        _freePin = GCHandle.Alloc((Action<nint, nuint>)((ptr, len) => Marshal.FreeHGlobal(ptr)));

        delegate* unmanaged[Cdecl]<byte*, byte**, nuint*, int> sourceCb = &ResolveSourceImpl;
        delegate* unmanaged[Cdecl]<byte*, byte**, nuint*, int> fileCb = &ResolveFileImpl;
        delegate* unmanaged[Cdecl]<byte*, nuint, void> freeCb = &FreeCallback;

        _handle = NativeMethods.typst_world_new(sourceCb, fileCb, freeCb);
    }

    [ThreadStatic]
    private static Func<string, byte[]?>? t_currentSourceResolver;

    [ThreadStatic]
    private static Func<string, byte[]?>? t_currentFileResolver;

    internal void SetResolvers()
    {
        t_currentSourceResolver = _sourceResolver;
        t_currentFileResolver = _fileResolver;
    }

    internal void ClearResolvers()
    {
        t_currentSourceResolver = null;
        t_currentFileResolver = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int ResolveSourceImpl(byte* pathPtr, byte** outBuf, nuint* outLen)
    {
        return ResolveImpl(t_currentSourceResolver, pathPtr, outBuf, outLen);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int ResolveFileImpl(byte* pathPtr, byte** outBuf, nuint* outLen)
    {
        return ResolveImpl(t_currentFileResolver, pathPtr, outBuf, outLen);
    }

    private static int ResolveImpl(Func<string, byte[]?>? resolver, byte* pathPtr, byte** outBuf, nuint* outLen)
    {
        if (resolver is null)
            return -2;

        try
        {
            var path = Marshal.PtrToStringUTF8((nint)pathPtr);
            if (path is null)
                return -2;

            var data = resolver(path);
            if (data is null)
                return -1;

            var ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);
            *outBuf = (byte*)ptr;
            *outLen = (nuint)data.Length;
            return 0;
        }
        catch
        {
            return -2;
        }
    }

    internal TypstWorldHandle* Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    internal void SetMain(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bytes = Encoding.UTF8.GetBytes(path + '\0');
        fixed (byte* ptr = bytes)
        {
            NativeMethods.typst_world_set_main(_handle, ptr);
        }
    }

    internal int AddFont(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        fixed (byte* ptr = data)
        {
            return NativeMethods.typst_world_add_font(_handle, ptr, (nuint)data.Length);
        }
    }

    internal int AddSystemFonts()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return NativeMethods.typst_world_add_system_fonts(_handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != null)
        {
            NativeMethods.typst_world_free(_handle);
            _handle = null;
        }

        if (_resolveSourcePin.IsAllocated) _resolveSourcePin.Free();
        if (_resolveFilePin.IsAllocated) _resolveFilePin.Free();
        if (_freePin.IsAllocated) _freePin.Free();
    }
}
