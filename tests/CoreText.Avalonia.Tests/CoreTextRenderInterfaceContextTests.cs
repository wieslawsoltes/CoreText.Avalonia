using Avalonia.Metal;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;

namespace CoreText.Avalonia.Tests;

public sealed class CoreTextRenderInterfaceContextTests
{
    [Fact]
    public void PublicFeaturesIncludeExternalObjectInteropWhenMetalContextIsPresent()
    {
        using var context = new CoreText.Avalonia.CoreTextRenderInterfaceContext(
            new CoreText.Avalonia.CoreTextPlatformOptions(),
            new StubMetalDevice());

        Assert.True(context.PublicFeatures.TryGetValue(typeof(IExternalObjectsRenderInterfaceContextFeature), out var externalObjects));
        Assert.True(context.PublicFeatures.TryGetValue(typeof(IExternalObjectsHandleWrapRenderInterfaceContextFeature), out var wrappedHandles));

        var feature = Assert.IsAssignableFrom<IExternalObjectsRenderInterfaceContextFeature>(externalObjects);
        Assert.Contains(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef, feature.SupportedImageHandleTypes);
        Assert.Contains(KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent, feature.SupportedSemaphoreTypes);
        Assert.Equal(
            CompositionGpuImportedImageSynchronizationCapabilities.Automatic |
            CompositionGpuImportedImageSynchronizationCapabilities.TimelineSemaphores,
            feature.GetSynchronizationCapabilities(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef));
        Assert.IsAssignableFrom<IExternalObjectsHandleWrapRenderInterfaceContextFeature>(wrappedHandles);
    }

    [Fact]
    public void PublicFeaturesStayEmptyWithoutMetalContext()
    {
        using var context = new CoreText.Avalonia.CoreTextRenderInterfaceContext(
            new CoreText.Avalonia.CoreTextPlatformOptions(),
            null);

        Assert.Empty(context.PublicFeatures);
        Assert.Null(context.TryGetFeature(typeof(IExternalObjectsRenderInterfaceContextFeature)));
    }

    private sealed class StubMetalDevice : IMetalDevice
    {
        public bool IsLost => false;

        public IDisposable EnsureCurrent() => NoopDisposable.Instance;

        public IntPtr Device => IntPtr.Zero;

        public IntPtr CommandQueue => IntPtr.Zero;

        public object? TryGetFeature(Type featureType) => null;

        public void Dispose()
        {
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}