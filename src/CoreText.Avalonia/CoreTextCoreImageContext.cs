using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Metal;
using CoreGraphics;

namespace CoreText.Avalonia;

internal sealed class CoreTextCoreImageContext : IDisposable
{
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string LibObjCLibrary = "/usr/lib/libobjc.A.dylib";
    private const uint Utf8Encoding = 0x08000100;
    private const int CfNumberDoubleType = 13;
    private static readonly IntPtr s_coreFoundationHandle = NativeLibrary.Load(CoreFoundationLibrary);

    private static readonly Lazy<CoreTextCoreImageContext> s_sharedSoftware = new(static () => new(IntPtr.Zero));
    private static readonly ConcurrentDictionary<nint, CoreTextCoreImageContext> s_metalContexts = new();

    private static readonly IntPtr s_ciContextClass = GetRequiredClass("CIContext");
    private static readonly IntPtr s_ciImageClass = GetRequiredClass("CIImage");
    private static readonly IntPtr s_ciFilterClass = GetRequiredClass("CIFilter");
    private static readonly IntPtr s_ciVectorClass = GetRequiredClass("CIVector");

    private static readonly IntPtr s_selContextWithOptions = GetRequiredSelector("contextWithOptions:");
    private static readonly IntPtr s_selContextWithMtlDeviceOptions = GetRequiredSelector("contextWithMTLDevice:options:");
    private static readonly IntPtr s_selImageWithCgImage = GetRequiredSelector("imageWithCGImage:");
    private static readonly IntPtr s_selFilterWithName = GetRequiredSelector("filterWithName:");
    private static readonly IntPtr s_selSetValueForKey = GetRequiredSelector("setValue:forKey:");
    private static readonly IntPtr s_selOutputImage = GetRequiredSelector("outputImage");
    private static readonly IntPtr s_selCreateCgImageFromRect = GetRequiredSelector("createCGImage:fromRect:");
    private static readonly IntPtr s_selOutputImageMaximumSize = GetRequiredSelector("outputImageMaximumSize");
    private static readonly IntPtr s_selClearCaches = GetRequiredSelector("clearCaches");
    private static readonly IntPtr s_selReclaimResources = GetRequiredSelector("reclaimResources");
    private static readonly IntPtr s_selImageByCroppingToRect = GetRequiredSelector("imageByCroppingToRect:");
    private static readonly IntPtr s_selImageByApplyingTransform = GetRequiredSelector("imageByApplyingTransform:");
    private static readonly IntPtr s_selVectorWithXYZW = GetRequiredSelector("vectorWithX:Y:Z:W:");

    private static readonly IntPtr s_filterGaussianBlur = CreateConstantString("CIGaussianBlur");
    private static readonly IntPtr s_filterColorMatrix = CreateConstantString("CIColorMatrix");
    private static readonly IntPtr s_filterSourceOver = CreateConstantString("CISourceOverCompositing");
    private static readonly IntPtr s_keyInputImage = CreateConstantString("inputImage");
    private static readonly IntPtr s_keyInputRadius = CreateConstantString("inputRadius");
    private static readonly IntPtr s_keyInputRVector = CreateConstantString("inputRVector");
    private static readonly IntPtr s_keyInputGVector = CreateConstantString("inputGVector");
    private static readonly IntPtr s_keyInputBVector = CreateConstantString("inputBVector");
    private static readonly IntPtr s_keyInputAVector = CreateConstantString("inputAVector");
    private static readonly IntPtr s_keyInputBiasVector = CreateConstantString("inputBiasVector");
    private static readonly IntPtr s_keyInputBackgroundImage = CreateConstantString("inputBackgroundImage");
    private static readonly IntPtr s_optionCacheIntermediates = CreateConstantString("kCIContextCacheIntermediates");
    private static readonly IntPtr s_optionPriorityRequestLow = CreateConstantString("kCIContextPriorityRequestLow");
    private static readonly IntPtr s_cfBooleanFalse = ReadConstantPointer("kCFBooleanFalse");
    private static readonly IntPtr s_cfBooleanTrue = ReadConstantPointer("kCFBooleanTrue");

    private readonly IntPtr _context;
    private readonly CGColorSpace _colorSpace;
    private readonly PixelSize _maxOutputPixelSize;

    public static CoreTextCoreImageContext SharedSoftware => s_sharedSoftware.Value;

    private CoreTextCoreImageContext(IntPtr metalDeviceHandle)
    {
        _colorSpace = CGColorSpace.CreateDeviceRGB();

        using var pool = new AutoreleasePool();
        var options = CreateContextOptions();
        IntPtr context;
        try
        {
            context = metalDeviceHandle == IntPtr.Zero
                ? IntPtr_objc_msgSend_IntPtr(s_ciContextClass, s_selContextWithOptions, options)
                : IntPtr_objc_msgSend_IntPtr_IntPtr(s_ciContextClass, s_selContextWithMtlDeviceOptions, metalDeviceHandle, options);
        }
        finally
        {
            if (options != IntPtr.Zero)
            {
                CFRelease(options);
            }
        }

        if (context == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to create a Core Image context.");
        }

        _context = objc_retain(context);
        _maxOutputPixelSize = ToPixelSize(CGSize_objc_msgSend(_context, s_selOutputImageMaximumSize));
    }

    public static CoreTextCoreImageContext ForMetalDevice(IMetalDevice device) =>
        s_metalContexts.GetOrAdd(device.Device, static handle => new CoreTextCoreImageContext(handle));

    public PixelSize MaxOutputPixelSize => _maxOutputPixelSize;

    public CoreTextBitmapImpl ApplyGaussianBlur(CoreTextBitmapImpl source, double radius)
    {
        using var pool = new AutoreleasePool();
        var extent = GetExtent(source);
        var sourceImage = CreateImage(source);
        var blurred = ApplyGaussianBlur(sourceImage, radius, extent);
        return RenderToNewBitmap(blurred, source);
    }

    public void BlurInPlace(CoreTextBitmapImpl bitmap, double radius)
    {
        if (radius <= 0)
        {
            return;
        }

        using var blurred = ApplyGaussianBlur(bitmap, radius);
        CoreTextBitmapOperations.CopyPixels(blurred, bitmap);
    }

    public CoreTextBitmapImpl ApplyDropShadow(CoreTextBitmapImpl source, Color color, double opacity, Vector offset, double blurRadius)
    {
        using var pool = new AutoreleasePool();
        var extent = GetExtent(source);
        var sourceImage = CreateImage(source);
        var shadowImage = CreateTintedAlphaImage(sourceImage, color, opacity);

        if (blurRadius > 0)
        {
            shadowImage = ApplyGaussianBlur(shadowImage, blurRadius, extent);
        }

        if (offset != default)
        {
            shadowImage = TranslateImage(shadowImage, offset);
        }

        shadowImage = CropImage(shadowImage, extent);

        var composite = CreateFilter(s_filterSourceOver);
        SetFilterValue(composite, sourceImage, s_keyInputImage);
        SetFilterValue(composite, shadowImage, s_keyInputBackgroundImage);
        var output = CropImage(GetFilterOutput(composite) != IntPtr.Zero ? GetFilterOutput(composite) : sourceImage, extent);
        return RenderToNewBitmap(output, source);
    }

    public void Dispose()
    {
        if (_context != IntPtr.Zero)
        {
            void_objc_msgSend(_context, s_selClearCaches);
            void_objc_msgSend(_context, s_selReclaimResources);
            objc_release(_context);
        }

        _colorSpace.Dispose();
    }

    private IntPtr CreateTintedAlphaImage(IntPtr sourceImage, Color color, double opacity)
    {
        var alphaScale = Math.Clamp(opacity, 0, 1) * (color.A / 255d);
        var red = color.R / 255d * alphaScale;
        var green = color.G / 255d * alphaScale;
        var blue = color.B / 255d * alphaScale;

        var filter = CreateFilter(s_filterColorMatrix);
        SetFilterValue(filter, sourceImage, s_keyInputImage);
        SetFilterValue(filter, CreateVector(0, 0, 0, red), s_keyInputRVector);
        SetFilterValue(filter, CreateVector(0, 0, 0, green), s_keyInputGVector);
        SetFilterValue(filter, CreateVector(0, 0, 0, blue), s_keyInputBVector);
        SetFilterValue(filter, CreateVector(0, 0, 0, alphaScale), s_keyInputAVector);
        SetFilterValue(filter, CreateVector(0, 0, 0, 0), s_keyInputBiasVector);
        return GetFilterOutput(filter) != IntPtr.Zero ? GetFilterOutput(filter) : sourceImage;
    }

    private static CGRect GetExtent(CoreTextBitmapImpl bitmap) =>
        new(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);

    private static IntPtr CreateImage(CoreTextBitmapImpl bitmap)
    {
        var cgImage = bitmap.CreateCGImageHandle();
        if (cgImage == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to create a Core Graphics image for Core Image.");
        }

        try
        {
            return IntPtr_objc_msgSend_IntPtr(s_ciImageClass, s_selImageWithCgImage, cgImage);
        }
        finally
        {
            CoreTextNative.CGImageRelease(cgImage);
        }
    }

    private IntPtr ApplyGaussianBlur(IntPtr image, double radius, CGRect extent)
    {
        if (radius <= 0)
        {
            return CropImage(image, extent);
        }

        var filter = CreateFilter(s_filterGaussianBlur);
        SetFilterValue(filter, image, s_keyInputImage);

        var radiusNumber = CreateDoubleNumber(radius);
        SetFilterValue(filter, radiusNumber, s_keyInputRadius);
        CFRelease(radiusNumber);

        var output = GetFilterOutput(filter);
        return CropImage(output != IntPtr.Zero ? output : image, extent);
    }

    private static IntPtr TranslateImage(IntPtr image, Vector offset)
    {
        var transform = new CGAffineTransform(
            1,
            0,
            0,
            1,
            (nfloat)offset.X,
            (nfloat)offset.Y);
        return IntPtr_objc_msgSend_CGAffineTransform(image, s_selImageByApplyingTransform, transform);
    }

    private CoreTextBitmapImpl RenderToNewBitmap(IntPtr image, CoreTextBitmapImpl template)
    {
        var output = new CoreTextBitmapImpl(
            template.PixelSize,
            template.Dpi,
            template.Format ?? PixelFormats.Bgra8888,
            template.AlphaFormat ?? AlphaFormat.Premul,
            scaleDrawingToDpiOnCreateDrawingContext: false,
            enableFontSmoothing: template.EnableFontSmoothing,
            enableSubpixelPositioning: template.EnableSubpixelPositioning,
            enableEffects: template.EnableEffects,
            coreImageContext: this);

        RenderToBitmap(image, output);
        return output;
    }

    private void RenderToBitmap(IntPtr image, CoreTextBitmapImpl output)
    {
        var extent = GetExtent(output);
        var cgImage = IntPtr_objc_msgSend_IntPtr_CGRect(_context, s_selCreateCgImageFromRect, image, extent);
        if (cgImage == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to render the Core Image output to a CGImage.");
        }

        try
        {
            output.ClearPixels();
            CoreTextNative.CGContextDrawImage(
                output.ContextHandle,
                new CoreTextNative.CGRect(0, 0, output.PixelSize.Width, output.PixelSize.Height),
                cgImage);
            output.IncrementVersion();
            void_objc_msgSend(_context, s_selClearCaches);
            void_objc_msgSend(_context, s_selReclaimResources);
        }
        finally
        {
            CoreTextNative.CGImageRelease(cgImage);
        }
    }

    private static IntPtr CropImage(IntPtr image, CGRect rect) =>
        IntPtr_objc_msgSend_CGRect(image, s_selImageByCroppingToRect, rect);

    private static IntPtr CreateVector(double x, double y, double z, double w) =>
        IntPtr_objc_msgSend_Double_Double_Double_Double(s_ciVectorClass, s_selVectorWithXYZW, x, y, z, w);

    private static IntPtr CreateFilter(IntPtr filterName) =>
        IntPtr_objc_msgSend_IntPtr(s_ciFilterClass, s_selFilterWithName, filterName) is var filter && filter != IntPtr.Zero
            ? filter
            : throw new InvalidOperationException("Unable to create the requested Core Image filter.");

    private static void SetFilterValue(IntPtr filter, IntPtr value, IntPtr key) =>
        void_objc_msgSend_IntPtr_IntPtr(filter, s_selSetValueForKey, value, key);

    private static IntPtr GetFilterOutput(IntPtr filter) =>
        IntPtr_objc_msgSend(filter, s_selOutputImage);

    private static IntPtr CreateDoubleNumber(double value) =>
        CFNumberCreate(IntPtr.Zero, CfNumberDoubleType, ref value);

    private static IntPtr GetRequiredClass(string name)
    {
        var value = objc_getClass(name);
        return value != IntPtr.Zero
            ? value
            : throw new InvalidOperationException($"Unable to resolve Objective-C class '{name}'.");
    }

    private static IntPtr GetRequiredSelector(string name)
    {
        var value = sel_registerName(name);
        return value != IntPtr.Zero
            ? value
            : throw new InvalidOperationException($"Unable to resolve Objective-C selector '{name}'.");
    }

    private static IntPtr CreateConstantString(string value)
    {
        var result = CFStringCreateWithCString(IntPtr.Zero, value, Utf8Encoding);
        return result != IntPtr.Zero
            ? result
            : throw new InvalidOperationException($"Unable to create Core Foundation string '{value}'.");
    }

    private static PixelSize ToPixelSize(CGSize size)
    {
        if (double.IsNaN(size.Width) || double.IsInfinity(size.Width) || size.Width <= 0 ||
            double.IsNaN(size.Height) || double.IsInfinity(size.Height) || size.Height <= 0)
        {
            return new PixelSize(16384, 16384);
        }

        return new PixelSize(
            Math.Max(1, (int)Math.Floor(size.Width)),
            Math.Max(1, (int)Math.Floor(size.Height)));
    }

    private static IntPtr CreateContextOptions()
    {
        var keys = new[] { s_optionCacheIntermediates, s_optionPriorityRequestLow };
        var values = new[] { s_cfBooleanFalse, s_cfBooleanTrue };
        return CFDictionaryCreate(IntPtr.Zero, keys, values, (nint)keys.Length, IntPtr.Zero, IntPtr.Zero);
    }

    private static IntPtr ReadConstantPointer(string symbol)
    {
        var export = NativeLibrary.GetExport(s_coreFoundationHandle, symbol);
        return Marshal.ReadIntPtr(export);
    }

    private readonly struct AutoreleasePool : IDisposable
    {
        private readonly IntPtr _handle;

        public AutoreleasePool()
        {
            _handle = objc_autoreleasePoolPush();
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                objc_autoreleasePoolPop(_handle);
            }
        }
    }

    [DllImport(LibObjCLibrary, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjCLibrary, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_retain")]
    private static extern IntPtr objc_retain(IntPtr value);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_release")]
    private static extern void objc_release(IntPtr value);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_autoreleasePoolPush")]
    private static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(LibObjCLibrary, EntryPoint = "objc_autoreleasePoolPop")]
    private static extern void objc_autoreleasePoolPop(IntPtr pool);

    [DllImport(CoreFoundationLibrary)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, uint encoding);

    [DllImport(CoreFoundationLibrary)]
    private static extern IntPtr CFNumberCreate(IntPtr allocator, int numberType, ref double value);

    [DllImport(CoreFoundationLibrary)]
    private static extern IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint numValues, IntPtr keyCallbacks, IntPtr valueCallbacks);

    [DllImport(CoreFoundationLibrary)]
    private static extern void CFRelease(IntPtr handle);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern CGSize CGSize_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_CGRect(IntPtr receiver, IntPtr selector, CGRect arg1);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_CGAffineTransform(IntPtr receiver, IntPtr selector, CGAffineTransform arg1);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_IntPtr_CGRect(IntPtr receiver, IntPtr selector, IntPtr arg1, CGRect arg2);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_Double_Double_Double_Double(
        IntPtr receiver,
        IntPtr selector,
        double arg1,
        double arg2,
        double arg3,
        double arg4);

    [DllImport(LibObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

}
