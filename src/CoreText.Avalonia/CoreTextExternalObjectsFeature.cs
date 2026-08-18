using System.Runtime.InteropServices;
using Avalonia.Metal;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using CoreFoundation;
using Foundation;
using IOSurface;
using Metal;
using ObjCRuntime;

namespace CoreText.Avalonia;

internal sealed class CoreTextExternalObjectsFeature : IExternalObjectsRenderInterfaceContextFeature,
    IExternalObjectsHandleWrapRenderInterfaceContextFeature
{
    private readonly IMetalDevice _metalDevice;
    private readonly byte[]? _deviceLuid;

    public CoreTextExternalObjectsFeature(IMetalDevice metalDevice)
    {
        _metalDevice = metalDevice;
        if (Runtime.GetINativeObject<IMTLDevice>(metalDevice.Device, false) is { } nativeDevice)
        {
            var bytes = BitConverter.GetBytes(nativeDevice.RegistryId);
            Array.Reverse(bytes);
            _deviceLuid = bytes;
        }
    }

    public IReadOnlyList<string> SupportedImageHandleTypes { get; } =
        [KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef];

    public IReadOnlyList<string> SupportedSemaphoreTypes { get; } =
        [KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent];

    public byte[]? DeviceUuid => null;

    public byte[]? DeviceLuid => _deviceLuid;

    public CompositionGpuImportedImageSynchronizationCapabilities GetSynchronizationCapabilities(string imageHandleType)
    {
        if (imageHandleType != KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef)
        {
            throw new NotSupportedException($"Image handle type '{imageHandleType}' is not supported.");
        }

         // Imported IOSurface images are snapshotted through CPU-visible surface memory and can also
         // be sequenced with Metal shared-event timeline values.
         return CompositionGpuImportedImageSynchronizationCapabilities.Automatic |
             CompositionGpuImportedImageSynchronizationCapabilities.TimelineSemaphores;
    }

    public IPlatformRenderInterfaceImportedImage ImportImage(IPlatformHandle handle, PlatformGraphicsExternalImageProperties properties)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.HandleDescriptor != KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef)
        {
            throw new NotSupportedException($"Image handle type '{handle.HandleDescriptor}' is not supported.");
        }

        return new ImportedImage(CreateOwnedSurface(handle), properties);
    }

    public IPlatformRenderInterfaceImportedImage ImportImage(ICompositionImportableSharedGpuContextImage image)
    {
        throw new NotSupportedException("Shared-context image import is not implemented for the CoreText backend.");
    }

    public IPlatformRenderInterfaceImportedSemaphore ImportSemaphore(IPlatformHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.HandleDescriptor != KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent)
        {
            throw new NotSupportedException($"Semaphore handle type '{handle.HandleDescriptor}' is not supported.");
        }

        return new ImportedSemaphore(CreateOwnedSharedEvent(handle));
    }

    public IExternalObjectsWrappedGpuHandle? WrapImageHandleOnAnyThread(IPlatformHandle handle,
        PlatformGraphicsExternalImageProperties properties)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.HandleDescriptor != KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef)
        {
            return null;
        }

        RetainCoreFoundationObject(handle.Handle);
        return new WrappedHandle(handle.Handle, handle.HandleDescriptor!, static ptr => ReleaseCoreFoundationObject(ptr));
    }

    public IExternalObjectsWrappedGpuHandle? WrapSemaphoreHandleOnAnyThread(IPlatformHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.HandleDescriptor != KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent)
        {
            return null;
        }

        RetainCoreFoundationObject(handle.Handle);
        return new WrappedHandle(handle.Handle, handle.HandleDescriptor!, static ptr => ReleaseCoreFoundationObject(ptr));
    }

    private static IOSurface.IOSurface CreateOwnedSurface(IPlatformHandle handle)
    {
        RetainCoreFoundationObject(handle.Handle);
        return Runtime.GetINativeObject<IOSurface.IOSurface>(handle.Handle, true)
            ?? throw new InvalidOperationException("Unable to wrap IOSurface handle.");
    }

    private static IMTLSharedEvent CreateOwnedSharedEvent(IPlatformHandle handle)
    {
        RetainCoreFoundationObject(handle.Handle);
        return Runtime.GetINativeObject<IMTLSharedEvent>(handle.Handle, true)
            ?? throw new InvalidOperationException("Unable to wrap MTLSharedEvent handle.");
    }

    private sealed class WrappedHandle(IntPtr handle, string descriptor, Action<IntPtr> releaseAction)
        : PlatformHandle(handle, descriptor), IExternalObjectsWrappedGpuHandle
    {
        private readonly Action<IntPtr> _releaseAction = releaseAction;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _releaseAction(Handle);
        }
    }

    private sealed class ImportedImage(IOSurface.IOSurface surface, PlatformGraphicsExternalImageProperties properties)
        : IPlatformRenderInterfaceImportedImage
    {
        private readonly IOSurface.IOSurface _surface = surface;
        private readonly PlatformGraphicsExternalImageProperties _properties = properties;

        public IBitmapImpl SnapshotWithKeyedMutex(uint acquireIndex, uint releaseIndex)
        {
            throw new NotSupportedException();
        }

        public IBitmapImpl SnapshotWithSemaphores(IPlatformRenderInterfaceImportedSemaphore waitForSemaphore,
            IPlatformRenderInterfaceImportedSemaphore signalSemaphore)
        {
            throw new NotSupportedException();
        }

        public IBitmapImpl SnapshotWithTimelineSemaphores(
            IPlatformRenderInterfaceImportedSemaphore waitForSemaphore,
            ulong waitForValue,
            IPlatformRenderInterfaceImportedSemaphore signalSemaphore,
            ulong signalValue)
        {
            var waitEvent = ((ImportedSemaphore)waitForSemaphore).Event;
            var signalEvent = ((ImportedSemaphore)signalSemaphore).Event;
            WaitForSharedEvent(waitEvent, waitForValue);

            try
            {
                return SnapshotWithAutomaticSync();
            }
            finally
            {
                signalEvent.SignaledValue = signalValue;
            }
        }

        public IBitmapImpl SnapshotWithAutomaticSync()
        {
            var pixelSize = new PixelSize(
                _properties.Width > 0 ? _properties.Width : (int)_surface.Width,
                _properties.Height > 0 ? _properties.Height : (int)_surface.Height);
            var format = _properties.Format switch
            {
                PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm => PixelFormats.Rgba8888,
                PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm => PixelFormats.Bgra8888,
                _ => throw new NotSupportedException("Pixel format is not supported.")
            };

            var bitmap = new CoreTextBitmapImpl(
                pixelSize,
                CoreTextPlatform.DefaultDpi,
                format,
                AlphaFormat.Premul,
                scaleDrawingToDpiOnCreateDrawingContext: false);

            _surface.Lock(IOSurfaceLockOptions.ReadOnly);
            try
            {
                CopySurfaceBytes(bitmap, pixelSize, _surface.BytesPerRow, _surface.BaseAddress, _properties.TopLeftOrigin);
            }
            finally
            {
                _surface.Unlock(IOSurfaceLockOptions.ReadOnly);
            }

            return bitmap;
        }

        public void Dispose()
        {
            _surface.Dispose();
        }

        private static unsafe void CopySurfaceBytes(CoreTextBitmapImpl target, PixelSize pixelSize, nint sourceBytesPerRow,
            IntPtr sourceAddress, bool topLeftOrigin)
        {
            var sourceStride = checked((int)sourceBytesPerRow);
            var targetStride = target.RowBytes;
            var copyBytesPerRow = Math.Min(sourceStride, targetStride);
            var source = (byte*)sourceAddress;
            var destination = (byte*)target.DataAddress;

            for (var row = 0; row < pixelSize.Height; row++)
            {
                var sourceRow = topLeftOrigin ? row : (pixelSize.Height - 1 - row);
                Buffer.MemoryCopy(
                    source + (sourceRow * sourceStride),
                    destination + (row * targetStride),
                    targetStride,
                    copyBytesPerRow);
            }
        }

        private static void WaitForSharedEvent(IMTLSharedEvent sharedEvent, ulong waitForValue)
        {
            if (sharedEvent.SignaledValue >= waitForValue)
            {
                return;
            }

            using var completed = new ManualResetEventSlim(false);
            using var listener = new MTLSharedEventListener(DispatchQueue.DefaultGlobalQueue);

            sharedEvent.NotifyListener(listener, waitForValue, (_, _) => completed.Set());

            if (sharedEvent.SignaledValue >= waitForValue)
            {
                return;
            }

            completed.Wait();
        }
    }

    private sealed class ImportedSemaphore(IMTLSharedEvent sharedEvent) : IPlatformRenderInterfaceImportedSemaphore
    {
        public IMTLSharedEvent Event { get; } = sharedEvent;

        public void Dispose()
        {
            Event.Dispose();
        }
    }

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFRetain")]
    private static extern IntPtr RetainCoreFoundationObject(IntPtr handle);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFRelease")]
    private static extern void ReleaseCoreFoundationObject(IntPtr handle);
}