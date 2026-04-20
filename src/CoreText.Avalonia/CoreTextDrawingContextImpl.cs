using Avalonia.Platform;
using System.Reflection;

namespace CoreText.Avalonia;

internal sealed class CoreTextDrawingContextImpl : IDrawingContextImpl, IDrawingContextImplWithEffects
{
    private static readonly PropertyInfo? s_imageBrushBitmapProperty =
        typeof(IImageBrushSource).GetProperty("Bitmap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

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
    private CoreTextBitmapImpl _bitmap;
    private double _opacity = 1;

    public CoreTextDrawingContextImpl(
        CoreTextBitmapImpl bitmap,
        Action? disposeAction = null,
        Action? afterDispose = null,
        bool scaleDrawingToDpi = true)
    {
        _bitmap = bitmap;
        _disposeAction = disposeAction;
        _afterDispose = afterDispose;
        _scaleDrawingToDpi = scaleDrawingToDpi;
        Transform = Matrix.Identity;
        InitializeBitmapContext(bitmap);
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
        if (source is not CoreTextBitmapImpl bitmap)
        {
            return;
        }

        using var image = bitmap.CreateCGImage();
        UsingState(() =>
        {
            ApplyTransform();
            CoreTextNative.CGContextSetAlpha(_bitmap.ContextHandle, opacity * _opacity);
            CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
            CoreTextNative.CGContextTranslateCTM(_bitmap.ContextHandle, destRect.X, destRect.Y + destRect.Height);
            CoreTextNative.CGContextScaleCTM(_bitmap.ContextHandle, 1, -1);
            CoreTextNative.CGContextDrawImage(_bitmap.ContextHandle, new CoreTextNative.CGRect(0, 0, destRect.Width, destRect.Height), image.Handle);
            CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
        });
    }

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect) =>
        DrawBitmap(source, 1, new Rect(source.PixelSize.ToSize(_bitmap.Dpi.X / 96d)), destRect);

    public void DrawLine(IPen? pen, Point p1, Point p2)
    {
        if (pen?.Brush is null)
        {
            return;
        }

        UsingState(() =>
        {
            ApplyTransform();
            ApplyPen(pen);
            CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
            CoreTextNative.CGContextMoveToPoint(_bitmap.ContextHandle, p1.X, p1.Y);
            CoreTextNative.CGContextAddLineToPoint(_bitmap.ContextHandle, p2.X, p2.Y);
            CoreTextNative.CGContextStrokePath(_bitmap.ContextHandle);
        });
    }

    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry)
    {
        if (geometry is not CoreTextGeometryImpl coreGeometry)
        {
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
        UsingState(() =>
        {
            ApplyTransform();
            FillAndStrokeCurrentPath(() =>
            {
                AddRoundedRect(rect);
                return FillRule.NonZero;
            }, brush, pen, rect.Rect);
        });
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
            CoreTextNative.CGContextSetRGBFillColor(_bitmap.ContextHandle, color.R / 255d, color.G / 255d, color.B / 255d, (color.A / 255d) * _opacity * foreground.Opacity);
            CoreTextNative.CGContextSetTextMatrix(_bitmap.ContextHandle, CoreTextNative.CGAffineTransform.MakeScale(1, -1));
            CoreTextNative.CTFontDrawGlyphs(font.Handle, coreGlyphRun.GlyphIndices, coreGlyphRun.Positions, coreGlyphRun.GlyphIndices.Length, _bitmap.ContextHandle);
        });
    }

    public IDrawingContextLayerImpl CreateLayer(PixelSize size) =>
        new CoreTextBitmapImpl(
            size,
            _bitmap.Dpi,
            PixelFormats.Bgra8888,
            AlphaFormat.Premul,
            scaleDrawingToDpiOnCreateDrawingContext: _scaleDrawingToDpi);

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
        var parent = _bitmap;
        var layer = new CoreTextBitmapImpl(parent.PixelSize, parent.Dpi, PixelFormats.Bgra8888, AlphaFormat.Premul);
        InitializeBitmapContext(layer);
        var layerLogicalSize = GetBitmapLogicalSize(layer);
        CoreTextNative.CGContextClearRect(layer.ContextHandle, new CoreTextNative.CGRect(0, 0, layerLogicalSize.Width, layerLogicalSize.Height));

        _statePops.Push(() =>
        {
            var current = _bitmap;
            CoreTextNative.CGContextRestoreGState(current.ContextHandle);
            _bitmap = parent;

            using var image = layer.CreateCGImage();
            UsingState(() =>
            {
                var logicalSize = GetBitmapLogicalSize(_bitmap);
                CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
                CoreTextNative.CGContextTranslateCTM(_bitmap.ContextHandle, 0, logicalSize.Height);
                CoreTextNative.CGContextScaleCTM(_bitmap.ContextHandle, 1, -1);
                CoreTextNative.CGContextSetAlpha(_bitmap.ContextHandle, _opacity);
                CoreTextNative.CGContextDrawImage(_bitmap.ContextHandle, new CoreTextNative.CGRect(0, 0, logicalSize.Width, logicalSize.Height), image.Handle);
                CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
            });
            current.Dispose();
        });

        _bitmap = layer;
    }

    public void PopLayer() => PopState();

    public void PushOpacity(double opacity, Rect? bounds)
    {
        _opacityStack.Push(_opacity);
        _opacity *= opacity;
    }

    public void PopOpacity()
    {
        if (_opacityStack.Count > 0)
        {
            _opacity = _opacityStack.Pop();
        }
    }

    public void PushOpacityMask(IBrush mask, Rect bounds)
    {
        _statePops.Push(static () => { });
    }

    public void PopOpacityMask() => PopState();

    public void PushGeometryClip(IGeometryImpl clip)
    {
        if (clip is not CoreTextGeometryImpl coreGeometry)
        {
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

    public void PushRenderOptions(RenderOptions renderOptions) => _renderOptions.Push(renderOptions);

    public void PopRenderOptions()
    {
        if (_renderOptions.Count > 0)
        {
            _renderOptions.Pop();
        }
    }

    public void PushTextOptions(TextOptions textOptions) => _textOptions.Push(textOptions);

    public void PopTextOptions()
    {
        if (_textOptions.Count > 0)
        {
            _textOptions.Pop();
        }
    }

    public object? GetFeature(Type t) => null;

    public void PushEffect(Rect? clipRect, IEffect effect)
    {
        PushLayer(clipRect ?? new Rect(_bitmap.PixelSize.ToSize(1)));
    }

    public void PopEffect() => PopLayer();

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

    private static void ConfigureAntialiasing(IntPtr context)
    {
        CoreTextNative.CGContextSetShouldAntialias(context, true);
        CoreTextNative.CGContextSetAllowsAntialiasing(context, true);
        CoreTextNative.CGContextSetAllowsFontSmoothing(context, true);
        CoreTextNative.CGContextSetShouldSmoothFonts(context, true);
        CoreTextNative.CGContextSetShouldSubpixelPositionFonts(context, true);
        CoreTextNative.CGContextSetShouldSubpixelQuantizeFonts(context, true);
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

    private void FillAndStrokeCurrentPath(Func<FillRule> addPath, IBrush? brush, IPen? pen, Rect bounds)
    {
        if (brush is not null)
        {
            FillPath(addPath, brush, bounds);
        }

        if (pen?.Brush is not null)
        {
            StrokePath(addPath, pen);
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
        }
    }

    private void StrokePath(Func<FillRule> addPath, IPen pen)
    {
        if (pen.Brush is null)
        {
            return;
        }

        CoreTextNative.CGContextBeginPath(_bitmap.ContextHandle);
        _ = addPath();
        ApplyPen(pen);
        CoreTextNative.CGContextDrawPath(_bitmap.ContextHandle, PathDrawingModeStroke);
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
        var bitmap = TryGetImageBrushBitmap(brush);
        if (bitmap is null)
        {
            return;
        }

        using var image = bitmap.CreateCGImage();
        var destRect = CalculateImageBrushDestRect(brush, bounds, bitmap.PixelSize.ToSize(1));
        CoreTextNative.CGContextSetAlpha(_bitmap.ContextHandle, _opacity * brush.Opacity);
        CoreTextNative.CGContextSaveGState(_bitmap.ContextHandle);
        CoreTextNative.CGContextTranslateCTM(_bitmap.ContextHandle, destRect.X, destRect.Y + destRect.Height);
        CoreTextNative.CGContextScaleCTM(_bitmap.ContextHandle, 1, -1);
        CoreTextNative.CGContextDrawImage(_bitmap.ContextHandle, new CoreTextNative.CGRect(0, 0, destRect.Width, destRect.Height), image.Handle);
        CoreTextNative.CGContextRestoreGState(_bitmap.ContextHandle);
    }

    private static CoreTextBitmapImpl? TryGetImageBrushBitmap(IImageBrush brush)
    {
        var source = brush.Source;
        if (source is null || s_imageBrushBitmapProperty is null)
        {
            return null;
        }

        var bitmapRef = s_imageBrushBitmapProperty.GetValue(source);
        if (bitmapRef is null)
        {
            return null;
        }

        var itemProperty = bitmapRef.GetType().GetProperty("Item", BindingFlags.Instance | BindingFlags.Public);
        return itemProperty?.GetValue(bitmapRef) as CoreTextBitmapImpl;
    }

    private static Rect CalculateImageBrushDestRect(IImageBrush brush, Rect bounds, Size sourceSize)
    {
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return bounds;
        }

        var scaleX = bounds.Width / sourceSize.Width;
        var scaleY = bounds.Height / sourceSize.Height;

        var scale = brush.Stretch switch
        {
            Stretch.None => 1,
            Stretch.Fill => double.NaN,
            Stretch.Uniform => Math.Min(scaleX, scaleY),
            Stretch.UniformToFill => Math.Max(scaleX, scaleY),
            _ => 1
        };

        if (double.IsNaN(scale))
        {
            return bounds;
        }

        var width = sourceSize.Width * scale;
        var height = sourceSize.Height * scale;
        var x = brush.AlignmentX switch
        {
            AlignmentX.Left => bounds.X,
            AlignmentX.Right => bounds.Right - width,
            _ => bounds.X + ((bounds.Width - width) / 2)
        };
        var y = brush.AlignmentY switch
        {
            AlignmentY.Top => bounds.Y,
            AlignmentY.Bottom => bounds.Bottom - height,
            _ => bounds.Y + ((bounds.Height - height) / 2)
        };

        return new Rect(x, y, width, height);
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
