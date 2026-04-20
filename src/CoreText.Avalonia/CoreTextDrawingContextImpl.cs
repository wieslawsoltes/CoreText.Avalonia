using Avalonia.Platform;

namespace CoreText.Avalonia;

internal sealed class CoreTextDrawingContextImpl : IDrawingContextImpl, IDrawingContextImplWithEffects, IDrawingContextWithAcrylicLikeSupport
{
    private const int PathDrawingModeFill = 0;
    private const int PathDrawingModeEvenOddFill = 1;
    private const int PathDrawingModeStroke = 2;
    private readonly Stack<Action> _statePops = new();
    private readonly Stack<double> _opacityStack = new();
    private readonly Stack<RenderOptions> _renderOptions = new();
    private readonly Stack<TextOptions> _textOptions = new();
    private readonly Action? _disposeAction;
    private readonly Action? _afterDispose;
    private readonly bool _scaleDrawingToDpi;
    private readonly bool _enableFontSmoothing;
    private readonly bool _enableSubpixelPositioning;
    private readonly bool _enableEffects;
    private CoreTextBitmapImpl _bitmap;
    private double _opacity = 1;
    private RenderOptions _currentRenderOptions;
    private TextOptions _currentTextOptions;

    public CoreTextDrawingContextImpl(
        CoreTextBitmapImpl bitmap,
        Action? disposeAction = null,
        Action? afterDispose = null,
        bool scaleDrawingToDpi = true,
        bool enableFontSmoothing = true,
        bool enableSubpixelPositioning = true,
        bool enableEffects = true,
        bool initializeBitmapContext = true)
    {
        _bitmap = bitmap;
        _disposeAction = disposeAction;
        _afterDispose = afterDispose;
        _scaleDrawingToDpi = scaleDrawingToDpi;
        _enableFontSmoothing = enableFontSmoothing;
        _enableSubpixelPositioning = enableSubpixelPositioning;
        _enableEffects = enableEffects;
        Transform = Matrix.Identity;
        if (initializeBitmapContext)
        {
            InitializeBitmapContext(bitmap);
        }
    }

    public Matrix Transform { get; set; }

    internal CoreTextBitmapImpl Bitmap => _bitmap;

    public void Clear(Color color)
    {
        UsingState(() =>
        {
            CoreTextNative.CGContextSetRGBFillColor(_bitmap.ContextHandle, color.R / 255d, color.G / 255d, color.B / 255d, color.A / 255d);
            var logicalSize = GetBitmapLogicalSize(_bitmap);
            CoreTextNative.CGContextFillRect(_bitmap.ContextHandle, new CoreTextNative.CGRect(0, 0, logicalSize.Width, logicalSize.Height));
        });
    }

    public void DrawBitmap(IBitmapImpl source, double opacity, Rect sourceRect, Rect destRect)
    {
        if (source is not CoreTextBitmapImpl bitmap || sourceRect.Width <= 0 || sourceRect.Height <= 0 || destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        UsingState(() =>
        {
            DrawBitmapCore(bitmap, opacity, sourceRect, destRect);
        });
    }

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect)
    {
        PushOpacityMask(opacityMask, opacityMaskRect);
        try
        {
            DrawBitmap(source, 1, new Rect(source.PixelSize.ToSize(_bitmap.Dpi.X / 96d)), destRect);
        }
        finally
        {
            PopOpacityMask();
        }
    }

    public void DrawLine(IPen? pen, Point p1, Point p2)
    {
        if (pen?.Brush is null)
        {
            return;
        }

        UsingState(() =>
        {
            ApplyTransform();
            StrokePath(() =>
            {
                CoreTextNative.CGContextMoveToPoint(_bitmap.ContextHandle, p1.X, p1.Y);
                CoreTextNative.CGContextAddLineToPoint(_bitmap.ContextHandle, p2.X, p2.Y);
                return FillRule.NonZero;
            }, pen, new Rect(p1, p2).Normalize().Inflate(pen.Thickness / 2));
        });
    }

    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry)
    {
        if (geometry is not CoreTextGeometryImpl coreGeometry)
        {
            return;
        }

        if (coreGeometry is CoreTextGeometryImpl.CombinedGeometry combined)
        {
            DrawCombinedGeometry(brush, pen, combined);
            return;
        }

        UsingState(() =>
        {
            ApplyTransform();
            FillAndStrokeCurrentPath(() =>
            {
                var builder = new CoreTextPathBuilder(_bitmap.ContextHandle);
                coreGeometry.Replay(builder);
                return builder.FillRule;
            }, brush, pen, coreGeometry.Bounds);
        });
    }

    public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows boxShadows = default)
    {
        foreach (var boxShadow in boxShadows)
        {
            if (boxShadow != default && !boxShadow.IsInset)
            {
                using var shadowBitmap = CreateRectangleShadow(rect, boxShadow);
                CompositeBitmapOntoCurrent(shadowBitmap);
            }
        }

        UsingState(() =>
        {
            ApplyTransform();
            FillAndStrokeCurrentPath(() =>
            {
                AddRoundedRect(rect);
                return FillRule.NonZero;
            }, brush, pen, rect.Rect);
        });

        foreach (var boxShadow in boxShadows)
        {
            if (boxShadow != default && boxShadow.IsInset)
            {
                using var shadowBitmap = CreateRectangleShadow(rect, boxShadow);
                CompositeBitmapOntoCurrent(shadowBitmap);
            }
        }
    }

    public void DrawRectangle(IExperimentalAcrylicMaterial material, RoundedRect rect)
    {
        var color = material.MaterialColor.A > 0 ? material.MaterialColor : material.FallbackColor;
        DrawRectangle(new SolidColorBrush(color), null, rect);
    }

    public void DrawRegion(IBrush? brush, IPen? pen, IPlatformRenderInterfaceRegion region)
    {
        if (region is not CoreTextRegionImpl coreRegion)
        {
            return;
        }

        foreach (var rect in coreRegion.Rects)
        {
            DrawRectangle(brush, pen, new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top));
        }
    }

    public void DrawEllipse(IBrush? brush, IPen? pen, Rect rect)
    {
        UsingState(() =>
        {
            ApplyTransform();
            FillAndStrokeCurrentPath(() =>
            {
                CoreTextNative.CGContextAddEllipseInRect(_bitmap.ContextHandle, new CoreTextNative.CGRect(rect.X, rect.Y, rect.Width, rect.Height));
                return FillRule.NonZero;
            }, brush, pen, rect);
        });
    }

    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun)
    {
        if (foreground is null || glyphRun is not CoreTextGlyphRunImpl coreGlyphRun)
        {
            return;
        }

        var color = TryGetSolidColor(foreground) ?? Colors.Black;
        using var font = coreGlyphRun.Typeface.CreateFont(coreGlyphRun.FontRenderingEmSize);

        UsingState(() =>
        {
            ApplyTransform();
            ConfigureTextRenderingState(_bitmap.ContextHandle);
            CoreTextNative.CGContextSetRGBFillColor(_bitmap.ContextHandle, color.R / 255d, color.G / 255d, color.B / 255d, (color.A / 255d) * _opacity * foreground.Opacity);
            CoreTextNative.CGContextSetTextMatrix(_bitmap.ContextHandle, CoreTextNative.CGAffineTransform.MakeScale(1, -1));

            if (_currentTextOptions.BaselinePixelAlignment != BaselinePixelAlignment.Unaligned)
            {
                var baselineShift = Math.Round(coreGlyphRun.BaselineOrigin.Y) - coreGlyphRun.BaselineOrigin.Y;
                if (Math.Abs(baselineShift) > double.Epsilon)
                {
                    CoreTextNative.CGContextTranslateCTM(_bitmap.ContextHandle, 0, -baselineShift);
                }
            }

            CoreTextNative.CTFontDrawGlyphs(font.Handle, coreGlyphRun.GlyphIndices, coreGlyphRun.Positions, coreGlyphRun.GlyphIndices.Length, _bitmap.ContextHandle);
        });
    }

    public IDrawingContextLayerImpl CreateLayer(PixelSize size) =>
        new CoreTextBitmapImpl(
            size,
            _bitmap.Dpi,
            PixelFormats.Bgra8888,
            AlphaFormat.Premul,
            scaleDrawingToDpiOnCreateDrawingContext: _scaleDrawingToDpi,
            enableFontSmoothing: _enableFontSmoothing,
            enableSubpixelPositioning: _enableSubpixelPositioning,
            enableEffects: _enableEffects,
            coreImageContext: _bitmap.CoreImageContext);

    public void PushClip(Rect clip)
    {
        var transformedClip = Transform == Matrix.Identity ? clip : clip.TransformToAABB(Transform);
        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
        CoreTextNative.CGContextAddRect(_bitmap.ContextHandle, new CoreTextNative.CGRect(transformedClip.X, transformedClip.Y, transformedClip.Width, transformedClip.Height));
        CoreTextNative.CGContextClip(_bitmap.ContextHandle);
        _statePops.Push(() => CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle));
    }

    public void PushClip(RoundedRect clip)
    {
        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        ApplyTransform();
        CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
        AddRoundedRect(clip);
        CoreTextNative.CGContextClip(_bitmap.ContextHandle);
        _statePops.Push(() => CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle));
    }

    public void PushClip(IPlatformRenderInterfaceRegion region)
    {
        if (region is not CoreTextRegionImpl coreRegion)
        {
            return;
        }

        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        ApplyTransform();
        CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
        foreach (var rect in coreRegion.Rects)
        {
            var r = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            CoreTextNative.CGContextAddRect(_bitmap.ContextHandle, new CoreTextNative.CGRect(r.X, r.Y, r.Width, r.Height));
        }

        CoreTextNative.CGContextClip(_bitmap.ContextHandle);
        _statePops.Push(() => CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle));
    }

    public void PopClip() => PopState();

    public void PushLayer(Rect bounds)
    {
        PushIsolatedLayer(layer =>
        {
            CompositeBitmapOntoCurrent(layer);
            layer.Dispose();
        });
    }

    public void PopLayer() => PopState();

    public void PushOpacity(double opacity, Rect? bounds)
    {
        _opacityStack.Push(_opacity);
        var combinedOpacity = Math.Clamp(_opacity * opacity, 0, 1);
        _opacity = 1;

        PushIsolatedLayer(layer =>
        {
            CoreTextBitmapOperations.MultiplyAlphaInPlace(layer, combinedOpacity);
            CompositeBitmapOntoCurrent(layer);
            layer.Dispose();
        });
    }

    public void PopOpacity()
    {
        if (_opacityStack.Count > 0)
        {
            PopState();
            _opacity = _opacityStack.Pop();
        }
    }

    public void PushOpacityMask(IBrush mask, Rect bounds)
    {
        var maskTransform = Transform;
        PushIsolatedLayer(layer =>
        {
            using var maskBitmap = CreateCompatibleBitmap();
            using (var maskContext = CreateChildContext(maskBitmap))
            {
                maskContext.Transform = maskTransform;
                maskContext.DrawRectangle(mask, null, new RoundedRect(bounds));
            }

            CoreTextBitmapOperations.MultiplyAlphaInPlace(layer, maskBitmap);
            CompositeBitmapOntoCurrent(layer);
            layer.Dispose();
        });
    }

    public void PopOpacityMask() => PopState();

    public void PushGeometryClip(IGeometryImpl clip)
    {
        if (clip is not CoreTextGeometryImpl coreGeometry)
        {
            return;
        }

        if (coreGeometry is CoreTextGeometryImpl.CombinedGeometry combined)
        {
            var clipTransform = Transform;
            PushIsolatedLayer(layer =>
            {
                using var maskBitmap = CreateCombinedMask(combined, pen: null, fill: true, clipTransform);
                CoreTextBitmapOperations.MultiplyAlphaInPlace(layer, maskBitmap);
                CompositeBitmapOntoCurrent(layer);
                layer.Dispose();
            });
            return;
        }

        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        ApplyTransform();
        CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
        var builder = new CoreTextPathBuilder(_bitmap.ContextHandle);
        coreGeometry.Replay(builder);
        if (builder.FillRule == FillRule.NonZero)
        {
            CoreTextNative.CGContextClip(_bitmap.ContextHandle);
        }
        else
        {
            CoreTextNative.CGContextEOClip(_bitmap.ContextHandle);
        }

        _statePops.Push(() => CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle));
    }

    public void PopGeometryClip() => PopState();

    public void PushRenderOptions(RenderOptions renderOptions)
    {
        _renderOptions.Push(_currentRenderOptions);
        _currentRenderOptions = _currentRenderOptions.MergeWith(renderOptions);
    }

    public void PopRenderOptions()
    {
        if (_renderOptions.Count > 0)
        {
            _currentRenderOptions = _renderOptions.Pop();
        }
    }

    public void PushTextOptions(TextOptions textOptions)
    {
        _textOptions.Push(_currentTextOptions);
        _currentTextOptions = _currentTextOptions.MergeWith(textOptions);
    }

    public void PopTextOptions()
    {
        if (_textOptions.Count > 0)
        {
            _currentTextOptions = _textOptions.Pop();
        }
    }

    public object? GetFeature(Type t) => null;

    public void PushEffect(Rect? clipRect, IEffect effect)
    {
        var effectTransform = Transform;
        PushIsolatedLayer(layer =>
        {
            using var output = ApplyEffect(layer, effect, clipRect, effectTransform);
            CompositeBitmapOntoCurrent(output);
            layer.Dispose();
        });
    }

    public void PopEffect() => PopState();

    public void Dispose()
    {
        while (_statePops.Count > 0)
        {
            PopState();
        }

        CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
        _disposeAction?.Invoke();
        _bitmap.IncrementVersion();
        _afterDispose?.Invoke();
    }

    private void PopState()
    {
        if (_statePops.Count == 0)
        {
            return;
        }

        var pop = _statePops.Pop();
        pop();
    }

    private void UsingState(Action action)
    {
        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        try
        {
            ConfigureGraphicsState(_bitmap.ContextHandle);
            action();
        }
        finally
        {
            CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
        }
    }

    private void ApplyTransform()
    {
        if (Transform == Matrix.Identity)
        {
            return;
        }

        CoreTextNative.CGContextConcatCTM(_bitmap.ContextHandle, CoreTextNative.CGAffineTransform.FromMatrix(Transform));
    }

    private void ConfigureAntialiasing()
    {
        ConfigureAntialiasing(_bitmap.ContextHandle);
    }

    private void ConfigureAntialiasing(IntPtr context)
    {
        var antialias = _currentRenderOptions.EdgeMode != EdgeMode.Aliased;
        var textMode = _currentTextOptions.TextRenderingMode;
        if (textMode == TextRenderingMode.Unspecified)
        {
            textMode = TextRenderingMode.SubpixelAntialias;
        }

        var fontSmoothing = _enableFontSmoothing && textMode == TextRenderingMode.SubpixelAntialias;
        var subpixel = _enableSubpixelPositioning && textMode == TextRenderingMode.SubpixelAntialias;

        CoreTextNative.CGContextSetShouldAntialias(context, antialias);
        CoreTextNative.CGContextSetAllowsAntialiasing(context, antialias);
        CoreTextNative.CGContextSetAllowsFontSmoothing(context, fontSmoothing);
        CoreTextNative.CGContextSetShouldSmoothFonts(context, fontSmoothing);
        CoreTextNative.CGContextSetShouldSubpixelPositionFonts(context, subpixel);
        CoreTextNative.CGContextSetShouldSubpixelQuantizeFonts(context, subpixel);
    }

    private void ConfigureGraphicsState(IntPtr context)
    {
        ConfigureAntialiasing(context);
        CoreTextNative.CGContextSetInterpolationQuality(context, GetInterpolationQuality(_currentRenderOptions.BitmapInterpolationMode));
    }

    private void ConfigureTextRenderingState(IntPtr context)
    {
        var textMode = _currentTextOptions.TextRenderingMode;
        if (textMode == TextRenderingMode.Unspecified)
        {
            textMode = TextRenderingMode.SubpixelAntialias;
        }

        var antialias = textMode != TextRenderingMode.Alias;
        var fontSmoothing = _enableFontSmoothing && textMode == TextRenderingMode.SubpixelAntialias;
        var subpixel = _enableSubpixelPositioning && textMode == TextRenderingMode.SubpixelAntialias;

        CoreTextNative.CGContextSetShouldAntialias(context, antialias);
        CoreTextNative.CGContextSetAllowsAntialiasing(context, antialias);
        CoreTextNative.CGContextSetAllowsFontSmoothing(context, fontSmoothing);
        CoreTextNative.CGContextSetShouldSmoothFonts(context, fontSmoothing);
        CoreTextNative.CGContextSetShouldSubpixelPositionFonts(context, subpixel);
        CoreTextNative.CGContextSetShouldSubpixelQuantizeFonts(context, subpixel);
    }

    private void InitializeBitmapContext(CoreTextBitmapImpl bitmap)
    {
        var scale = GetBitmapScale(bitmap);
        CoreTextNative.CGContextSaveGState(bitmap.ContextHandle);
        CoreTextNative.CGContextTranslateCTM(bitmap.ContextHandle, 0, bitmap.PixelSize.Height);
        CoreTextNative.CGContextScaleCTM(bitmap.ContextHandle, scale.X, -scale.Y);
        ConfigureAntialiasing(bitmap.ContextHandle);
    }

    private Vector GetBitmapScale(CoreTextBitmapImpl bitmap)
    {
        if (!_scaleDrawingToDpi)
        {
            return new Vector(1, 1);
        }

        return new Vector(Math.Max(bitmap.Dpi.X / 96d, 0.0001), Math.Max(bitmap.Dpi.Y / 96d, 0.0001));
    }

    private Size GetBitmapLogicalSize(CoreTextBitmapImpl bitmap)
    {
        var scale = GetBitmapScale(bitmap);
        return new Size(bitmap.PixelSize.Width / scale.X, bitmap.PixelSize.Height / scale.Y);
    }

    private void DrawCombinedGeometry(IBrush? brush, IPen? pen, CoreTextGeometryImpl.CombinedGeometry combined)
    {
        if (combined.Bounds.Width <= 0 || combined.Bounds.Height <= 0)
        {
            return;
        }

        if (brush is not null)
        {
            using var fillMask = CreateCombinedMask(combined, pen: null, fill: true, Transform);
            using var fillBitmap = CreateCompatibleBitmap();
            using (var fillContext = CreateChildContext(fillBitmap))
            {
                fillContext.Transform = Transform;
                fillContext.DrawRectangle(brush, null, new RoundedRect(combined.Bounds));
            }

            CoreTextBitmapOperations.MultiplyAlphaInPlace(fillBitmap, fillMask);
            CompositeBitmapOntoCurrent(fillBitmap);
        }

        if (pen?.Brush is not null)
        {
            using var strokeMask = CreateCombinedMask(combined, pen, fill: false, Transform);
            var strokeBounds = combined.Bounds.Inflate(new Thickness(pen.Thickness / 2));
            using var strokeBitmap = CreateCompatibleBitmap();
            using (var strokeContext = CreateChildContext(strokeBitmap))
            {
                strokeContext.Transform = Transform;
                strokeContext.DrawRectangle(pen.Brush, null, new RoundedRect(strokeBounds));
            }

            CoreTextBitmapOperations.MultiplyAlphaInPlace(strokeBitmap, strokeMask);
            CompositeBitmapOntoCurrent(strokeBitmap);
        }
    }

    private CoreTextBitmapImpl CreateCombinedMask(CoreTextGeometryImpl.CombinedGeometry combined, IPen? pen, bool fill, Matrix transform)
    {
        var leftMask = RenderGeometryMask(combined.Left, pen, fill, transform);
        using var rightMask = RenderGeometryMask(combined.Right, pen, fill, transform);
        CoreTextBitmapOperations.CombineMasksInPlace(leftMask, rightMask, combined.CombineMode);
        return leftMask;
    }

    private CoreTextBitmapImpl RenderGeometryMask(CoreTextGeometryImpl geometry, IPen? pen, bool fill, Matrix transform)
    {
        var mask = CreateCompatibleBitmap();
        using var maskContext = CreateChildContext(mask);
        maskContext.Transform = transform;
        if (fill)
        {
            maskContext.DrawGeometry(Brushes.White, null, geometry);
        }
        else if (pen is not null)
        {
            maskContext.DrawGeometry(null, CreateMaskPen(pen), geometry);
        }

        return mask;
    }

    private static IPen CreateMaskPen(IPen pen) =>
        new Pen(Brushes.White, pen.Thickness, pen.DashStyle, pen.LineCap, pen.LineJoin, pen.MiterLimit);

    private CoreTextBitmapImpl CreateCompatibleBitmap() => CreateCompatibleBitmap(GetBitmapLogicalSize(_bitmap));

    private CoreTextBitmapImpl CreateCompatibleBitmap(Size logicalSize)
    {
        var scale = GetBitmapScale(_bitmap);
        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(logicalSize.Width * scale.X)),
            Math.Max(1, (int)Math.Ceiling(logicalSize.Height * scale.Y)));

        var bitmap = new CoreTextBitmapImpl(
            pixelSize,
            _bitmap.Dpi,
            _bitmap.Format ?? PixelFormats.Bgra8888,
            _bitmap.AlphaFormat ?? AlphaFormat.Premul,
            scaleDrawingToDpiOnCreateDrawingContext: _scaleDrawingToDpi,
            enableFontSmoothing: _enableFontSmoothing,
            enableSubpixelPositioning: _enableSubpixelPositioning,
            enableEffects: _enableEffects,
            coreImageContext: _bitmap.CoreImageContext);
        InitializeBitmapContext(bitmap);
        var bitmapLogicalSize = GetBitmapLogicalSize(bitmap);
        CoreTextNative.CGContextClearRect(bitmap.ContextHandle, new CoreTextNative.CGRect(0, 0, bitmapLogicalSize.Width, bitmapLogicalSize.Height));
        return bitmap;
    }

    private CoreTextDrawingContextImpl CreateChildContext(CoreTextBitmapImpl bitmap) =>
        new(
            bitmap,
            scaleDrawingToDpi: _scaleDrawingToDpi,
            enableFontSmoothing: _enableFontSmoothing,
            enableSubpixelPositioning: _enableSubpixelPositioning,
            enableEffects: _enableEffects,
            initializeBitmapContext: false);

    private void PushIsolatedLayer(Action<CoreTextBitmapImpl> popAction)
    {
        var parent = _bitmap;
        var layer = CreateCompatibleBitmap();

        _statePops.Push(() =>
        {
            var current = _bitmap;
            CoreTextNative.CGContextRestoreGState(current.ContextHandle);
            _bitmap = parent;
            popAction(current);
        });

        _bitmap = layer;
    }

    private void CompositeBitmapOntoCurrent(CoreTextBitmapImpl source)
    {
        CoreTextBitmapOperations.CompositeSourceOver(_bitmap, source);
    }

    private void DrawBitmapCore(CoreTextBitmapImpl bitmap, double opacity, Rect sourceRect, Rect destRect, bool flipX = false, bool flipY = false, bool applyCurrentTransform = true)
    {
        var blendMode = _currentRenderOptions.BitmapBlendingMode;
        if (blendMode == BitmapBlendingMode.Destination)
        {
            return;
        }

        var imageHandle = bitmap.CreateCGImageHandle(sourceRect);
        if (imageHandle == IntPtr.Zero)
        {
            return;
        }

        if (applyCurrentTransform)
        {
            ApplyTransform();
        }

        try
        {
            CoreTextNative.CGContextSetAlpha(_bitmap.ContextHandle, opacity * _opacity);
            CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
            CoreTextNative.CGContextSetBlendMode(_bitmap.ContextHandle, GetBlendMode(blendMode));
            CoreTextNative.CGContextSetInterpolationQuality(_bitmap.ContextHandle, GetInterpolationQuality(_currentRenderOptions.BitmapInterpolationMode));
            var translateX = destRect.X + (flipX ? destRect.Width : 0);
            var translateY = destRect.Y + (flipY ? 0 : destRect.Height);
            CoreTextNative.CGContextTranslateCTM(_bitmap.ContextHandle, translateX, translateY);
            CoreTextNative.CGContextScaleCTM(_bitmap.ContextHandle, flipX ? -1 : 1, flipY ? 1 : -1);
            CoreTextNative.CGContextDrawImage(_bitmap.ContextHandle, new CoreTextNative.CGRect(0, 0, destRect.Width, destRect.Height), imageHandle);
            CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
        }
        finally
        {
            CoreTextNative.CFRelease(imageHandle);
        }
    }

    private void FillAndStrokeCurrentPath(Func<FillRule> addPath, IBrush? brush, IPen? pen, Rect bounds)
    {
        if (brush is not null)
        {
            FillPath(addPath, brush, bounds);
        }

        if (pen?.Brush is not null)
        {
            StrokePath(addPath, pen, bounds.Inflate(new Thickness(pen.Thickness / 2)));
        }
    }

    private void FillPath(Func<FillRule> addPath, IBrush brush, Rect bounds)
    {
        CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
        var fillRule = addPath();

        if (TryGetSolidColor(brush) is { } color)
        {
            CoreTextNative.CGContextSetRGBFillColor(_bitmap.ContextHandle, color.R / 255d, color.G / 255d, color.B / 255d, (color.A / 255d) * _opacity * brush.Opacity);
            CoreTextNative.CGContextDrawPath(_bitmap.ContextHandle, fillRule == FillRule.EvenOdd ? PathDrawingModeEvenOddFill : PathDrawingModeFill);
            return;
        }

        if (brush is ILinearGradientBrush linear)
        {
            DrawClippedCurrentPath(fillRule, () => DrawLinearGradient(linear, bounds));
            return;
        }

        if (brush is IRadialGradientBrush radial)
        {
            DrawClippedCurrentPath(fillRule, () => DrawRadialGradient(radial, bounds));
            return;
        }

        if (brush is IImageBrush image)
        {
            DrawClippedCurrentPath(fillRule, () => DrawImageBrush(image, bounds));
            return;
        }

        if (brush is IConicGradientBrush conic)
        {
            DrawClippedCurrentPath(fillRule, () => DrawConicGradient(conic, bounds));
            return;
        }

        if (brush is ISceneBrush sceneBrush)
        {
            using var content = sceneBrush.CreateContent();
            if (content is not null)
            {
                DrawClippedCurrentPath(fillRule, () => DrawSceneBrushContent(content, bounds));
            }

            return;
        }

        if (brush is ISceneBrushContent sceneContent)
        {
            DrawClippedCurrentPath(fillRule, () => DrawSceneBrushContent(sceneContent, bounds));
        }
    }

    private void StrokePath(Func<FillRule> addPath, IPen pen, Rect bounds)
    {
        if (pen.Brush is null)
        {
            return;
        }

        CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
        _ = addPath();

        if (TryGetSolidColor(pen.Brush) is { })
        {
            ApplyPen(pen);
            CoreTextNative.CGContextDrawPath(_bitmap.ContextHandle, PathDrawingModeStroke);
            return;
        }

        var path = CoreTextNative.CGContextCopyPath(_bitmap.ContextHandle);
        if (path == IntPtr.Zero)
        {
            return;
        }

        try
        {
            using var strokeMask = CreateCompatibleBitmap();
            using (var maskContext = CreateChildContext(strokeMask))
            {
                maskContext.Transform = Transform;
                maskContext.DrawStrokedMaskPath(path, pen);
            }

            using var strokeBitmap = CreateCompatibleBitmap();
            using (var strokeContext = CreateChildContext(strokeBitmap))
            {
                strokeContext.Transform = Transform;
                strokeContext.DrawRectangle(pen.Brush, null, new RoundedRect(bounds));
            }

            CoreTextBitmapOperations.MultiplyAlphaInPlace(strokeBitmap, strokeMask);
            CompositeBitmapOntoCurrent(strokeBitmap);
        }
        finally
        {
            CoreTextNative.CGPathRelease(path);
        }
    }

    private void DrawClippedCurrentPath(FillRule fillRule, Action action)
    {
        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        try
        {
            if (fillRule == FillRule.EvenOdd)
            {
                CoreTextNative.CGContextEOClip(_bitmap.ContextHandle);
            }
            else
            {
                CoreTextNative.CGContextClip(_bitmap.ContextHandle);
            }

            action();
        }
        finally
        {
            CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
        }
    }

    private void ApplyPen(IPen pen)
    {
        if (TryGetSolidColor(pen.Brush) is not { } color)
        {
            return;
        }

        CoreTextNative.CGContextSetRGBStrokeColor(_bitmap.ContextHandle, color.R / 255d, color.G / 255d, color.B / 255d, (color.A / 255d) * _opacity * (pen.Brush?.Opacity ?? 1));
        CoreTextNative.CGContextSetLineWidth(_bitmap.ContextHandle, pen.Thickness);
        CoreTextNative.CGContextSetLineJoin(_bitmap.ContextHandle, pen.LineJoin switch
        {
            PenLineJoin.Round => 1,
            PenLineJoin.Bevel => 2,
            _ => 0
        });
        CoreTextNative.CGContextSetLineCap(_bitmap.ContextHandle, pen.LineCap switch
        {
            PenLineCap.Round => 1,
            PenLineCap.Square => 2,
            _ => 0
        });
        CoreTextNative.CGContextSetMiterLimit(_bitmap.ContextHandle, pen.MiterLimit);

        if (pen.DashStyle?.Dashes is { Count: > 0 } dashes)
        {
            var dashArray = dashes.Select(static x => x).ToArray();
            CoreTextNative.CGContextSetLineDash(_bitmap.ContextHandle, pen.DashStyle.Offset, dashArray, dashArray.Length);
        }
    }

    private void DrawLinearGradient(ILinearGradientBrush brush, Rect bounds)
    {
        var gradient = CreateGradient(brush.GradientStops, brush.Opacity * _opacity);
        if (gradient == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var start = brush.StartPoint.ToPixels(bounds);
            var end = brush.EndPoint.ToPixels(bounds);
            CoreTextNative.CGContextDrawLinearGradient(_bitmap.ContextHandle, gradient, new CoreTextNative.CGPoint(start.X, start.Y), new CoreTextNative.CGPoint(end.X, end.Y), 3);
        }
        finally
        {
            CoreTextNative.CFRelease(gradient);
        }
    }

    private void DrawRadialGradient(IRadialGradientBrush brush, Rect bounds)
    {
        var gradient = CreateGradient(brush.GradientStops, brush.Opacity * _opacity);
        if (gradient == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var center = brush.Center.ToPixels(bounds);
            var origin = brush.GradientOrigin.ToPixels(bounds);
            var radius = Math.Max(brush.RadiusX.ToValue(bounds.Width), brush.RadiusY.ToValue(bounds.Height));
            CoreTextNative.CGContextDrawRadialGradient(_bitmap.ContextHandle, gradient, new CoreTextNative.CGPoint(origin.X, origin.Y), 0, new CoreTextNative.CGPoint(center.X, center.Y), radius, 3);
        }
        finally
        {
            CoreTextNative.CFRelease(gradient);
        }
    }

    private void DrawImageBrush(IImageBrush brush, Rect bounds)
    {
        using var bitmap = CreateImageBrushBitmap(brush);
        if (bitmap is null)
        {
            return;
        }

        var logicalSourceSize = new Size(
            bitmap.PixelSize.Width / Math.Max(bitmap.Dpi.X / 96d, 0.0001),
            bitmap.PixelSize.Height / Math.Max(bitmap.Dpi.Y / 96d, 0.0001));
        var sourceRect = brush.SourceRect.ToPixels(logicalSourceSize).Intersect(new Rect(logicalSourceSize));
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return;
        }

        var tileRect = brush.DestinationRect.ToPixels(bounds);
        if (tileRect.Width <= 0 || tileRect.Height <= 0)
        {
            return;
        }

        DrawTiledBitmap(
            bitmap,
            bounds,
            tileRect,
            sourceRect,
            brush.TileMode,
            brush.Transform,
            brush.TransformOrigin,
            brush.Opacity,
            brush.Stretch,
            brush.AlignmentX,
            brush.AlignmentY,
            useContentAlignment: true);
    }

    private static CoreTextBitmapImpl? CreateImageBrushBitmap(IImageBrush brush)
    {
        if (brush.Source is not global::Avalonia.Media.Imaging.Bitmap source)
        {
            return null;
        }

        var bitmap = new CoreTextBitmapImpl(
            source.PixelSize,
            source.Dpi,
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var framebuffer = bitmap.Lock())
        {
            source.CopyPixels(framebuffer);
        }

        return bitmap;
    }

    private static Rect CalculateImageBrushContentRect(IImageBrush brush, Rect tileRect, Size sourceSize) =>
        CalculateTileBrushContentRect(brush.Stretch, brush.AlignmentX, brush.AlignmentY, tileRect, sourceSize);

    private static Rect CalculateTileBrushContentRect(Stretch stretch, AlignmentX alignmentX, AlignmentY alignmentY, Rect tileRect, Size sourceSize)
    {
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return tileRect;
        }

        var scale = stretch.CalculateScaling(tileRect.Size, sourceSize);
        var contentSize = sourceSize * scale;
        var translate = CalculateAlignmentTranslate(alignmentX, alignmentY, contentSize, tileRect.Size);
        return new Rect(tileRect.Position + translate, contentSize);
    }

    private static Vector CalculateAlignmentTranslate(AlignmentX alignmentX, AlignmentY alignmentY, Size sourceSize, Size destinationSize)
    {
        var x = alignmentX switch
        {
            AlignmentX.Center => (destinationSize.Width - sourceSize.Width) / 2,
            AlignmentX.Right => destinationSize.Width - sourceSize.Width,
            _ => 0
        };

        var y = alignmentY switch
        {
            AlignmentY.Center => (destinationSize.Height - sourceSize.Height) / 2,
            AlignmentY.Bottom => destinationSize.Height - sourceSize.Height,
            _ => 0
        };

        return new Vector(x, y);
    }

    private void DrawSceneBrushContent(ISceneBrushContent content, Rect bounds)
    {
        var tileRect = content.Brush.DestinationRect.ToPixels(bounds);
        if (tileRect.Width <= 0 || tileRect.Height <= 0)
        {
            return;
        }

        var sourceRect = content.Brush.SourceRect.ToPixels(content.Rect);
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return;
        }

        using var tileBitmap = CreateCompatibleBitmap(tileRect.Size);
        using (var tileContext = CreateChildContext(tileBitmap))
        {
            tileContext.Clear(Colors.Transparent);
            var scale = content.Brush.Stretch.CalculateScaling(tileRect.Size, sourceRect.Size);
            var translate = CalculateAlignmentTranslate(content.Brush.AlignmentX, content.Brush.AlignmentY, sourceRect.Size * scale, tileRect.Size);
            var renderTransform =
                Matrix.CreateTranslation(-sourceRect.X, -sourceRect.Y) *
                Matrix.CreateScale(scale) *
                Matrix.CreateTranslation(translate);
            content.Render(tileContext, renderTransform);
        }

        DrawTiledBitmap(
            tileBitmap,
            bounds,
            tileRect,
            new Rect(GetBitmapLogicalSize(tileBitmap)),
            content.Brush.TileMode,
            content.Brush.Transform,
            content.Brush.TransformOrigin,
            content.Brush.Opacity,
            content.Brush.Stretch,
            content.Brush.AlignmentX,
            content.Brush.AlignmentY,
            useContentAlignment: false);
    }

    private void DrawConicGradient(IConicGradientBrush brush, Rect bounds)
    {
        if (brush.GradientStops.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var gradientBitmap = RenderConicGradientBitmap(brush, bounds);

        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        try
        {
            if (brush.Transform is { } transform)
            {
                var origin = brush.TransformOrigin.ToPixels(bounds);
                var offset = Matrix.CreateTranslation(origin);
                var brushTransform = (-offset) * transform.Value * offset;
                CoreTextNative.CGContextConcatCTM(_bitmap.ContextHandle, CoreTextNative.CGAffineTransform.FromMatrix(brushTransform));
            }

            DrawBitmapCore(
                gradientBitmap,
                brush.Opacity,
                new Rect(GetBitmapLogicalSize(gradientBitmap)),
                bounds,
                applyCurrentTransform: false);
        }
        finally
        {
            CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
        }
    }

    private void DrawTiledBitmap(
        CoreTextBitmapImpl bitmap,
        Rect bounds,
        Rect tileRect,
        Rect sourceRect,
        TileMode tileMode,
        ITransform? brushTransform,
        RelativePoint transformOrigin,
        double opacity,
        Stretch stretch,
        AlignmentX alignmentX,
        AlignmentY alignmentY,
        bool useContentAlignment)
    {
        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        try
        {
            if (brushTransform is { } transform)
            {
                var origin = transformOrigin.ToPixels(bounds);
                var offset = Matrix.CreateTranslation(origin);
                var appliedTransform = (-offset) * transform.Value * offset;
                CoreTextNative.CGContextConcatCTM(_bitmap.ContextHandle, CoreTextNative.CGAffineTransform.FromMatrix(appliedTransform));
            }

            if (tileMode == TileMode.None)
            {
                var destRect = useContentAlignment
                    ? CalculateTileBrushContentRect(stretch, alignmentX, alignmentY, tileRect, sourceRect.Size)
                    : tileRect;
                DrawBitmapCore(bitmap, opacity, sourceRect, destRect, applyCurrentTransform: false);
                return;
            }

            var startColumn = (int)Math.Floor((bounds.X - tileRect.X) / tileRect.Width);
            var endColumn = (int)Math.Ceiling((bounds.Right - tileRect.X) / tileRect.Width);
            var startRow = (int)Math.Floor((bounds.Y - tileRect.Y) / tileRect.Height);
            var endRow = (int)Math.Ceiling((bounds.Bottom - tileRect.Y) / tileRect.Height);

            for (var row = startRow; row < endRow; row++)
            {
                for (var column = startColumn; column < endColumn; column++)
                {
                    var cell = new Rect(
                        tileRect.X + (column * tileRect.Width),
                        tileRect.Y + (row * tileRect.Height),
                        tileRect.Width,
                        tileRect.Height);
                    var contentRect = useContentAlignment
                        ? CalculateTileBrushContentRect(stretch, alignmentX, alignmentY, cell, sourceRect.Size)
                        : cell;
                    var flipX = (tileMode is TileMode.FlipX or TileMode.FlipXY) && Math.Abs(column) % 2 == 1;
                    var flipY = (tileMode is TileMode.FlipY or TileMode.FlipXY) && Math.Abs(row) % 2 == 1;
                    DrawBitmapCore(bitmap, opacity, sourceRect, contentRect, flipX, flipY, applyCurrentTransform: false);
                }
            }
        }
        finally
        {
            CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
        }
    }

    private void DrawStrokedMaskPath(IntPtr path, IPen pen)
    {
        UsingState(() =>
        {
            ApplyTransform();
            CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
            CoreTextNative.CGContextAddPath(_bitmap.ContextHandle, path);
            ApplyPen(CreateMaskPen(pen));
            CoreTextNative.CGContextDrawPath(_bitmap.ContextHandle, PathDrawingModeStroke);
        });
    }

    private CoreTextBitmapImpl RenderConicGradientBitmap(IConicGradientBrush brush, Rect bounds)
    {
        var bitmap = CreateCompatibleBitmap(bounds.Size);
        var logicalSize = GetBitmapLogicalSize(bitmap);
        var center = brush.Center.ToPixels(bounds) - bounds.Position;
        var stops = brush.GradientStops.OrderBy(static x => x.Offset).ToArray();
        var scaleX = logicalSize.Width / bitmap.PixelSize.Width;
        var scaleY = logicalSize.Height / bitmap.PixelSize.Height;

        unsafe
        {
            for (var y = 0; y < bitmap.PixelSize.Height; y++)
            {
                var row = (uint*)(bitmap.DataAddress + (y * bitmap.RowBytes));
                var localY = (y + 0.5) * scaleY;

                for (var x = 0; x < bitmap.PixelSize.Width; x++)
                {
                    var localX = (x + 0.5) * scaleX;
                    var angle = Math.Atan2(localY - center.Y, localX - center.X) * (180d / Math.PI);
                    var normalizedAngle = (angle + 90d - brush.Angle) % 360d;
                    if (normalizedAngle < 0)
                    {
                        normalizedAngle += 360d;
                    }

                    var t = ApplyGradientSpread(normalizedAngle / 360d, brush.SpreadMethod);
                    row[x] = PackPremultipliedColor(InterpolateGradientColor(stops, t));
                }
            }
        }

        return bitmap;
    }

    private static double ApplyGradientSpread(double t, GradientSpreadMethod spreadMethod)
    {
        return spreadMethod switch
        {
            GradientSpreadMethod.Pad => Math.Clamp(t, 0, 1),
            GradientSpreadMethod.Repeat => t - Math.Floor(t),
            GradientSpreadMethod.Reflect => ReflectGradientCoordinate(t),
            _ => Math.Clamp(t, 0, 1)
        };
    }

    private static double ReflectGradientCoordinate(double t)
    {
        var whole = Math.Floor(t);
        var fraction = t - whole;
        return ((long)whole & 1) == 0 ? fraction : 1 - fraction;
    }

    private static Color InterpolateGradientColor(IReadOnlyList<IGradientStop> stops, double t)
    {
        if (stops.Count == 0)
        {
            return Colors.Transparent;
        }

        if (t <= stops[0].Offset)
        {
            return stops[0].Color;
        }

        for (var i = 1; i < stops.Count; i++)
        {
            if (t <= stops[i].Offset)
            {
                var left = stops[i - 1];
                var right = stops[i];
                var range = Math.Max(right.Offset - left.Offset, double.Epsilon);
                var factor = (t - left.Offset) / range;
                return LerpColor(left.Color, right.Color, factor);
            }
        }

        return stops[^1].Color;
    }

    private static Color LerpColor(Color left, Color right, double factor)
    {
        byte Lerp(byte a, byte b) => (byte)Math.Clamp((int)Math.Round(a + ((b - a) * factor)), 0, 255);
        return Color.FromArgb(
            Lerp(left.A, right.A),
            Lerp(left.R, right.R),
            Lerp(left.G, right.G),
            Lerp(left.B, right.B));
    }

    private static uint PackPremultipliedColor(Color color)
    {
        var alpha = color.A;
        var red = (byte)((color.R * alpha + 127) / 255);
        var green = (byte)((color.G * alpha + 127) / 255);
        var blue = (byte)((color.B * alpha + 127) / 255);
        return ((uint)alpha << 24) | ((uint)red << 16) | ((uint)green << 8) | blue;
    }

    private static int GetInterpolationQuality(BitmapInterpolationMode interpolationMode) => interpolationMode switch
    {
        BitmapInterpolationMode.None => 1,
        BitmapInterpolationMode.Unspecified or BitmapInterpolationMode.LowQuality => 2,
        BitmapInterpolationMode.MediumQuality => 4,
        BitmapInterpolationMode.HighQuality => 3,
        _ => 0
    };

    private static int GetBlendMode(BitmapBlendingMode blendingMode) => blendingMode switch
    {
        BitmapBlendingMode.Unspecified or BitmapBlendingMode.SourceOver => 0,
        BitmapBlendingMode.Multiply => 1,
        BitmapBlendingMode.Screen => 2,
        BitmapBlendingMode.Overlay => 3,
        BitmapBlendingMode.Darken => 4,
        BitmapBlendingMode.Lighten => 5,
        BitmapBlendingMode.ColorDodge => 6,
        BitmapBlendingMode.ColorBurn => 7,
        BitmapBlendingMode.SoftLight => 8,
        BitmapBlendingMode.HardLight => 9,
        BitmapBlendingMode.Difference => 10,
        BitmapBlendingMode.Exclusion => 11,
        BitmapBlendingMode.Hue => 12,
        BitmapBlendingMode.Saturation => 13,
        BitmapBlendingMode.Color => 14,
        BitmapBlendingMode.Luminosity => 15,
        BitmapBlendingMode.Source => 17,
        BitmapBlendingMode.SourceIn => 18,
        BitmapBlendingMode.SourceOut => 19,
        BitmapBlendingMode.SourceAtop => 20,
        BitmapBlendingMode.DestinationOver => 21,
        BitmapBlendingMode.DestinationIn => 22,
        BitmapBlendingMode.DestinationOut => 23,
        BitmapBlendingMode.DestinationAtop => 24,
        BitmapBlendingMode.Xor => 25,
        BitmapBlendingMode.Plus => 27,
        _ => 0
    };

    private CoreTextBitmapImpl ApplyEffect(CoreTextBitmapImpl layer, IEffect effect, Rect? clipRect, Matrix transform)
    {
        CoreTextBitmapImpl output;

        if (effect is IBlurEffect blur && _enableEffects && blur.Radius > 0)
        {
            output = (_bitmap.CoreImageContext ?? CoreTextCoreImageContext.SharedSoftware)
                .ApplyGaussianBlur(layer, GetEffectRadiusPixels(blur.Radius));
        }
        else if (effect is IDropShadowEffect drop && _enableEffects)
        {
            output = (_bitmap.CoreImageContext ?? CoreTextCoreImageContext.SharedSoftware).ApplyDropShadow(
                layer,
                drop.Color,
                drop.Opacity,
                new Vector(drop.OffsetX, drop.OffsetY),
                GetEffectRadiusPixels(drop.BlurRadius));
        }
        else
        {
            output = CoreTextBitmapOperations.Clone(layer);
        }

        if (clipRect.HasValue)
        {
            using var clipMask = CreateRectMask(clipRect.Value, transform);
            CoreTextBitmapOperations.MultiplyAlphaInPlace(output, clipMask);
        }

        return output;
    }
    private CoreTextBitmapImpl CreateRectangleShadow(RoundedRect rect, BoxShadow shadow)
    {
        if (shadow.IsInset)
        {
            using var originalMask = RenderRoundedRectMask(rect, Transform);
            var mask = CoreTextBitmapOperations.Clone(originalMask);
            using var holeMask = RenderRoundedRectMask(
                rect.Deflate(shadow.Spread, shadow.Spread),
                Transform * Matrix.CreateTranslation(shadow.OffsetX, shadow.OffsetY));
            CoreTextBitmapOperations.CombineMasksInPlace(mask, holeMask, GeometryCombineMode.Exclude);
            if (_enableEffects && shadow.Blur > 0)
            {
                (_bitmap.CoreImageContext ?? CoreTextCoreImageContext.SharedSoftware)
                    .BlurInPlace(mask, GetEffectRadiusPixels(shadow.Blur));
            }

            using var clipMask = RenderRoundedRectMask(rect, Transform);
            CoreTextBitmapOperations.MultiplyAlphaInPlace(mask, clipMask);
            using (mask)
            {
                return CoreTextBitmapOperations.CreateTintedFromAlphaMask(mask, shadow.Color, _opacity);
            }
        }

        var outsetMask = RenderRoundedRectMask(
            rect.Inflate(shadow.Spread, shadow.Spread),
            Transform * Matrix.CreateTranslation(shadow.OffsetX, shadow.OffsetY));
        if (_enableEffects && shadow.Blur > 0)
        {
            (_bitmap.CoreImageContext ?? CoreTextCoreImageContext.SharedSoftware)
                .BlurInPlace(outsetMask, GetEffectRadiusPixels(shadow.Blur));
        }

        using (outsetMask)
        {
            return CoreTextBitmapOperations.CreateTintedFromAlphaMask(outsetMask, shadow.Color, _opacity);
        }
    }

    private CoreTextBitmapImpl RenderRoundedRectMask(RoundedRect rect, Matrix transform)
    {
        var mask = CreateCompatibleBitmap();
        using var maskContext = CreateChildContext(mask);
        maskContext.Transform = transform;
        maskContext.DrawRectangle(Brushes.White, null, rect);
        return mask;
    }

    private CoreTextBitmapImpl CreateRectMask(Rect rect, Matrix transform)
    {
        var mask = CreateCompatibleBitmap();
        using var maskContext = CreateChildContext(mask);
        maskContext.Transform = transform;
        maskContext.DrawRectangle(Brushes.White, null, new RoundedRect(rect));
        return mask;
    }

    private int GetEffectRadiusPixels(double radius)
    {
        if (!_enableEffects)
        {
            return 0;
        }

        var scale = GetBitmapScale(_bitmap);
        var averageScale = (scale.X + scale.Y) / 2;
        return Math.Max(1, (int)Math.Ceiling(radius * averageScale));
    }

    private static void CompositeInto(CoreTextBitmapImpl destination, CoreTextBitmapImpl source)
    {
        CoreTextBitmapOperations.CompositeSourceOver(destination, source);
    }

    private static IntPtr CreateGradient(IReadOnlyList<IGradientStop> stops, double opacity)
    {
        if (stops.Count == 0)
        {
            return IntPtr.Zero;
        }

        var components = new double[stops.Count * 4];
        var locations = new double[stops.Count];

        for (var i = 0; i < stops.Count; i++)
        {
            var color = stops[i].Color;
            components[(i * 4) + 0] = color.R / 255d;
            components[(i * 4) + 1] = color.G / 255d;
            components[(i * 4) + 2] = color.B / 255d;
            components[(i * 4) + 3] = (color.A / 255d) * opacity;
            locations[i] = stops[i].Offset;
        }

        return CoreTextNative.CreateGradient(components, locations, stops.Count);
    }

    private static Color? TryGetSolidColor(IBrush? brush) => brush switch
    {
        ISolidColorBrush solid => solid.Color,
        _ => null
    };

    private void AddRoundedRect(RoundedRect roundedRect)
    {
        var rect = roundedRect.Rect;
        if (!roundedRect.IsRounded)
        {
            CoreTextNative.CGContextAddRect(_bitmap.ContextHandle, new CoreTextNative.CGRect(rect.X, rect.Y, rect.Width, rect.Height));
            return;
        }

        var radius = Math.Min(
            Math.Min(roundedRect.RadiiTopLeft.X, roundedRect.RadiiTopRight.X),
            Math.Min(roundedRect.RadiiBottomLeft.X, roundedRect.RadiiBottomRight.X));

        if (radius <= 0)
        {
            CoreTextNative.CGContextAddRect(_bitmap.ContextHandle, new CoreTextNative.CGRect(rect.X, rect.Y, rect.Width, rect.Height));
            return;
        }

        CoreTextNative.CGContextMoveToPoint(_bitmap.ContextHandle, rect.X + radius, rect.Y);
        CoreTextNative.CGContextAddLineToPoint(_bitmap.ContextHandle, rect.Right - radius, rect.Y);
        CoreTextNative.CGContextAddArcToPoint(_bitmap.ContextHandle, rect.Right, rect.Y, rect.Right, rect.Y + radius, radius);
        CoreTextNative.CGContextAddLineToPoint(_bitmap.ContextHandle, rect.Right, rect.Bottom - radius);
        CoreTextNative.CGContextAddArcToPoint(_bitmap.ContextHandle, rect.Right, rect.Bottom, rect.Right - radius, rect.Bottom, radius);
        CoreTextNative.CGContextAddLineToPoint(_bitmap.ContextHandle, rect.X + radius, rect.Bottom);
        CoreTextNative.CGContextAddArcToPoint(_bitmap.ContextHandle, rect.X, rect.Bottom, rect.X, rect.Bottom - radius, radius);
        CoreTextNative.CGContextAddLineToPoint(_bitmap.ContextHandle, rect.X, rect.Y + radius);
        CoreTextNative.CGContextAddArcToPoint(_bitmap.ContextHandle, rect.X, rect.Y, rect.X + radius, rect.Y, radius);
        CoreTextNative.CGContextClosePath(_bitmap.ContextHandle);
    }
}
