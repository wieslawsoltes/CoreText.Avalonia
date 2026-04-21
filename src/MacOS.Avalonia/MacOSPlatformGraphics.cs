using System.Runtime.InteropServices;
using Avalonia.Metal;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Surfaces;

namespace MacOS.Avalonia;

internal sealed class MacOSMetalPlatformGraphics : IPlatformGraphics
{
    private readonly IMTLDevice _device;
    private readonly IMTLCommandQueue _commandQueue;

    public MacOSMetalPlatformGraphics(IMTLDevice device)
    {
        _device = device;
        _commandQueue = _device.CreateCommandQueue()
            ?? throw new InvalidOperationException("Unable to create a Metal command queue.");
    }

    public bool UsesSharedContext => false;

    public IPlatformGraphicsContext CreateContext() => new MacOSMetalDevice(_device, _commandQueue);

    public IPlatformGraphicsContext GetSharedContext() => throw new NotSupportedException();
}

internal sealed class MacOSMetalDevice(IMTLDevice device, IMTLCommandQueue commandQueue) : IMetalDevice
{
    private readonly IMTLDevice _device = device;
    private readonly IMTLCommandQueue _commandQueue = commandQueue;

    public bool IsLost => false;

    public IDisposable EnsureCurrent() => NoopDisposable.Instance;

    public IntPtr Device => _device.Handle;

    public IntPtr CommandQueue => _commandQueue.Handle;

    public object? TryGetFeature(Type featureType) => null;

    public void Dispose()
    {
    }

    public IMTLDevice NativeDevice => _device;

    public IMTLCommandQueue NativeCommandQueue => _commandQueue;
}

internal sealed class MacOSOpenGlPlatformGraphics : IPlatformGraphics, IPlatformGraphicsOpenGlContextFactory
{
    private static readonly IntPtr OpenGlLibrary = NativeLibrary.Load("/System/Library/Frameworks/OpenGL.framework/OpenGL");
    private readonly NSOpenGLPixelFormat _pixelFormat;
    private readonly GlVersion _version = new(GlProfileType.OpenGL, 4, 1, false);
    private readonly GlInterface _glInterface;
    private readonly int _sampleCount;
    private readonly int _stencilSize;
    private readonly MacOSGlContext _sharedContext;

    public MacOSOpenGlPlatformGraphics()
    {
        var attributes = new NSOpenGLPixelFormatAttribute[]
        {
            NSOpenGLPixelFormatAttribute.Accelerated,
            NSOpenGLPixelFormatAttribute.DoubleBuffer,
            NSOpenGLPixelFormatAttribute.ColorSize, (NSOpenGLPixelFormatAttribute)32,
            NSOpenGLPixelFormatAttribute.AlphaSize, (NSOpenGLPixelFormatAttribute)8,
            NSOpenGLPixelFormatAttribute.DepthSize, (NSOpenGLPixelFormatAttribute)24,
            NSOpenGLPixelFormatAttribute.StencilSize, (NSOpenGLPixelFormatAttribute)8,
            (NSOpenGLPixelFormatAttribute)0
        };

        _pixelFormat = new NSOpenGLPixelFormat(attributes)
            ?? throw new InvalidOperationException("Unable to create an NSOpenGLPixelFormat.");
        _sampleCount = 0;
        _stencilSize = 8;
        _glInterface = new GlInterface(_version, GetProcAddress);
        _sharedContext = CreateSharedContextCore(null);
    }

    public bool UsesSharedContext => true;

    public IPlatformGraphicsContext CreateContext() => CreateSharedContextCore(null);

    public IPlatformGraphicsContext GetSharedContext() => _sharedContext;

    public IGlContext CreateContext(IEnumerable<GlVersion>? versions) => CreateSharedContextCore(null);

    private MacOSGlContext CreateSharedContextCore(MacOSGlContext? sharedWith)
    {
        var native = new NSOpenGLContext(_pixelFormat, sharedWith?.NativeContext);
        return new MacOSGlContext(native, _pixelFormat, _glInterface, _version, _sampleCount, _stencilSize, sharedWith);
    }

    private static IntPtr GetProcAddress(string proc)
    {
        return NativeLibrary.TryGetExport(OpenGlLibrary, proc, out var address) ? address : IntPtr.Zero;
    }
}

internal sealed class MacOSGlContext : IGlContext
{
    private readonly NSOpenGLPixelFormat _pixelFormat;
    private readonly MacOSGlContext? _sharedWith;

    public MacOSGlContext(
        NSOpenGLContext nativeContext,
        NSOpenGLPixelFormat pixelFormat,
        GlInterface glInterface,
        GlVersion version,
        int sampleCount,
        int stencilSize,
        MacOSGlContext? sharedWith)
    {
        NativeContext = nativeContext;
        _pixelFormat = pixelFormat;
        _sharedWith = sharedWith;
        GlInterface = glInterface;
        Version = version;
        SampleCount = sampleCount;
        StencilSize = stencilSize;
    }

    public NSOpenGLContext NativeContext { get; }

    public GlVersion Version { get; }

    public GlInterface GlInterface { get; }

    public int SampleCount { get; }

    public int StencilSize { get; }

    public bool IsLost => false;

    public IDisposable EnsureCurrent() => MakeCurrent();

    public IDisposable MakeCurrent()
    {
        NativeContext.MakeCurrentContext();
        return new GlCurrentContextScope();
    }

    public bool IsSharedWith(IGlContext context)
    {
        if (ReferenceEquals(this, context))
        {
            return true;
        }

        if (context is not MacOSGlContext other)
        {
            return false;
        }

        return ReferenceEquals(_sharedWith, other)
            || ReferenceEquals(other._sharedWith, this)
            || (_sharedWith is not null && ReferenceEquals(_sharedWith, other._sharedWith));
    }

    public bool CanCreateSharedContext => true;

    public IGlContext? CreateSharedContext(IEnumerable<GlVersion>? preferredVersions = null)
    {
        return new MacOSGlContext(
            new NSOpenGLContext(_pixelFormat, NativeContext),
            _pixelFormat,
            GlInterface,
            Version,
            SampleCount,
            StencilSize,
            this);
    }

    public object? TryGetFeature(Type featureType) => null;

    public void Dispose()
    {
        NativeContext.Dispose();
    }

    private sealed class GlCurrentContextScope : IDisposable
    {
        public void Dispose()
        {
            NSOpenGLContext.ClearCurrentContext();
        }
    }
}

internal sealed class NoopDisposable : IDisposable
{
    public static readonly NoopDisposable Instance = new();

    public void Dispose()
    {
    }
}
