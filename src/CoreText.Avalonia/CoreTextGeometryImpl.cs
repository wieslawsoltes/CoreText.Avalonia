using System.Diagnostics.CodeAnalysis;
using Avalonia.Media;
using Avalonia.Platform;

namespace CoreText.Avalonia;

internal abstract class CoreTextGeometryImpl : IGeometryImpl
{
    public abstract Rect Bounds { get; }

    public virtual double ContourLength => 0;

    public virtual Rect GetRenderBounds(IPen? pen) => pen is null ? Bounds : Bounds.Inflate(pen.Thickness / 2);

    public virtual IGeometryImpl GetWidenedGeometry(IPen pen) => this;

    public virtual bool FillContains(Point point) => Bounds.Contains(point);

    public virtual IGeometryImpl? Intersect(IGeometryImpl geometry)
    {
        var rect = Bounds.Intersect(geometry.Bounds);
        return rect.Width <= 0 || rect.Height <= 0 ? null : CreateRectangle(rect);
    }

    public virtual bool StrokeContains(IPen? pen, Point point) => GetRenderBounds(pen).Contains(point);

    public virtual ITransformedGeometryImpl WithTransform(Matrix transform) => new CoreTextTransformedGeometryImpl(this, transform);

    public virtual bool TryGetPointAtDistance(double distance, out Point point)
    {
        point = default;
        return false;
    }

    public virtual bool TryGetPointAndTangentAtDistance(double distance, out Point point, out Point tangent)
    {
        point = default;
        tangent = default;
        return false;
    }

    public virtual bool TryGetSegment(double startDistance, double stopDistance, bool startOnBeginFigure, [NotNullWhen(true)] out IGeometryImpl? segmentGeometry)
    {
        segmentGeometry = null;
        return false;
    }

    public abstract void Replay(CoreTextPathBuilder pathBuilder);

    public static CoreTextGeometryImpl CreateRectangle(Rect rect) => new PrimitiveGeometry(rect, static (builder, geometryBounds) => builder.AddRect(geometryBounds), rect);

    public static CoreTextGeometryImpl CreateEllipse(Rect rect) => new PrimitiveGeometry(rect, static (builder, geometryBounds) => builder.AddEllipse(geometryBounds), rect);

    public static CoreTextGeometryImpl CreateLine(Point p1, Point p2)
    {
        var bounds = new Rect(
            new Point(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y)),
            new Point(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y)));
        return new PrimitiveGeometry(bounds, (builder, _) =>
        {
            builder.BeginFigure(p1);
            builder.LineTo(p2);
            builder.EndFigure(false);
        }, bounds);
    }

    public static CoreTextGeometryImpl CreateGroup(FillRule fillRule, IReadOnlyList<IGeometryImpl> children) => new GroupGeometry(fillRule, children.OfType<CoreTextGeometryImpl>().ToArray());

    public static CoreTextGeometryImpl CreateCombined(GeometryCombineMode combineMode, IGeometryImpl g1, IGeometryImpl g2) => new CombinedGeometry(combineMode, ToCore(g1), ToCore(g2));

    public static CoreTextGeometryImpl CreateGlyphRun(GlyphRun glyphRun) => new GlyphRunGeometry(glyphRun);

    public static CoreTextGeometryImpl ToCore(IGeometryImpl geometry) => geometry as CoreTextGeometryImpl ?? CreateRectangle(geometry.Bounds);

    private sealed class PrimitiveGeometry : CoreTextGeometryImpl
    {
        private readonly Action<CoreTextPathBuilder, Rect> _replay;

        public PrimitiveGeometry(Rect bounds, Action<CoreTextPathBuilder, Rect> replay, Rect replayBounds)
        {
            Bounds = bounds;
            _replay = replay;
        }

        public override Rect Bounds { get; }

        public override void Replay(CoreTextPathBuilder pathBuilder) => _replay(pathBuilder, Bounds);
    }

    private sealed class GroupGeometry : CoreTextGeometryImpl
    {
        private readonly CoreTextGeometryImpl[] _children;

        public GroupGeometry(FillRule fillRule, CoreTextGeometryImpl[] children)
        {
            FillRule = fillRule;
            _children = children;
            Bounds = _children.Length == 0 ? default : _children.Select(static x => x.Bounds).Aggregate(static (left, right) => left.Union(right));
        }

        public FillRule FillRule { get; }

        public override Rect Bounds { get; }

        public override void Replay(CoreTextPathBuilder pathBuilder)
        {
            pathBuilder.FillRule = FillRule;
            foreach (var child in _children)
            {
                child.Replay(pathBuilder);
            }
        }
    }

    private sealed class CombinedGeometry : CoreTextGeometryImpl
    {
        private readonly GeometryCombineMode _combineMode;
        private readonly CoreTextGeometryImpl _left;
        private readonly CoreTextGeometryImpl _right;

        public CombinedGeometry(GeometryCombineMode combineMode, CoreTextGeometryImpl left, CoreTextGeometryImpl right)
        {
            _combineMode = combineMode;
            _left = left;
            _right = right;

            Bounds = combineMode switch
            {
                GeometryCombineMode.Intersect => left.Bounds.Intersect(right.Bounds),
                GeometryCombineMode.Exclude => left.Bounds,
                _ => left.Bounds.Union(right.Bounds)
            };
        }

        public override Rect Bounds { get; }

        public override void Replay(CoreTextPathBuilder pathBuilder)
        {
            if (_combineMode == GeometryCombineMode.Intersect)
            {
                pathBuilder.AddRect(_left.Bounds.Intersect(_right.Bounds));
                return;
            }

            _left.Replay(pathBuilder);

            if (_combineMode != GeometryCombineMode.Exclude)
            {
                _right.Replay(pathBuilder);
            }
        }
    }

    private sealed class GlyphRunGeometry : CoreTextGeometryImpl
    {
        private readonly GlyphRun _glyphRun;

        public GlyphRunGeometry(GlyphRun glyphRun)
        {
            _glyphRun = glyphRun;
            Bounds = glyphRun.Bounds;
        }

        public override Rect Bounds { get; }

        public override void Replay(CoreTextPathBuilder pathBuilder)
        {
            if (_glyphRun.GlyphTypeface.PlatformTypeface is not CoreTextPlatformTypeface platformTypeface)
            {
                pathBuilder.AddRect(Bounds);
                return;
            }

            using var font = platformTypeface.CreateFont(_glyphRun.FontRenderingEmSize);
            var currentX = _glyphRun.BaselineOrigin.X;
            var baselineY = _glyphRun.BaselineOrigin.Y;

            foreach (var glyphInfo in _glyphRun.GlyphInfos)
            {
                var transform = CoreTextNative.CGAffineTransform.MakeTranslation(currentX + glyphInfo.GlyphOffset.X, baselineY + glyphInfo.GlyphOffset.Y);
                var path = CoreTextNative.CTFontCreatePathForGlyph(font.Handle, glyphInfo.GlyphIndex, ref transform);
                if (path != IntPtr.Zero)
                {
                    pathBuilder.AddNativePath(path);
                    CoreTextNative.CFRelease(path);
                }

                currentX += glyphInfo.GlyphAdvance;
            }
        }
    }
}

internal sealed class CoreTextTransformedGeometryImpl : CoreTextGeometryImpl, ITransformedGeometryImpl
{
    public CoreTextTransformedGeometryImpl(CoreTextGeometryImpl sourceGeometry, Matrix transform)
    {
        SourceGeometry = sourceGeometry;
        Transform = transform;
    }

    public override Rect Bounds => SourceGeometry.Bounds.TransformToAABB(Transform);

    public IGeometryImpl SourceGeometry { get; }

    public Matrix Transform { get; }

    public override void Replay(CoreTextPathBuilder pathBuilder)
    {
        pathBuilder.PushTransform(Transform);
        ((CoreTextGeometryImpl)SourceGeometry).Replay(pathBuilder);
        pathBuilder.PopTransform();
    }
}

internal sealed class CoreTextStreamGeometryImpl : CoreTextGeometryImpl, IStreamGeometryImpl
{
    private readonly List<GeometryCommand> _commands = new();
    private FillRule _fillRule = FillRule.EvenOdd;
    private Rect _bounds;

    public override Rect Bounds => _bounds;

    public IStreamGeometryImpl Clone()
    {
        var clone = new CoreTextStreamGeometryImpl();
        clone._commands.AddRange(_commands);
        clone._fillRule = _fillRule;
        clone._bounds = _bounds;
        return clone;
    }

    public IStreamGeometryContextImpl Open() => new Context(this);

    public override void Replay(CoreTextPathBuilder pathBuilder)
    {
        pathBuilder.FillRule = _fillRule;
        foreach (var command in _commands)
        {
            command(pathBuilder);
        }
    }

    private void AddPoint(Point point)
    {
        _bounds = _bounds == default ? new Rect(point, point) : _bounds.Union(new Rect(point, point));
    }

    private delegate void GeometryCommand(CoreTextPathBuilder builder);

    private sealed class Context : IStreamGeometryContextImpl
    {
        private readonly CoreTextStreamGeometryImpl _owner;
        private Point _currentPoint;

        public Context(CoreTextStreamGeometryImpl owner)
        {
            _owner = owner;
        }

        public void SetFillRule(FillRule fillRule) => _owner._fillRule = fillRule;

        public void BeginFigure(Point startPoint, bool isFilled = true)
        {
            _currentPoint = startPoint;
            _owner.AddPoint(startPoint);
            _owner._commands.Add(builder => builder.BeginFigure(startPoint));
        }

        public void LineTo(Point point, bool isStroked = true)
        {
            _currentPoint = point;
            _owner.AddPoint(point);
            _owner._commands.Add(builder => builder.LineTo(point));
        }

        public void ArcTo(Point point, Size size, double rotationAngle, bool isLargeArc, SweepDirection sweepDirection, bool isStroked = true)
        {
            _currentPoint = point;
            _owner.AddPoint(point);
            _owner._commands.Add(builder => builder.LineTo(point));
        }

        public void CubicBezierTo(Point controlPoint1, Point controlPoint2, Point endPoint, bool isStroked = true)
        {
            _currentPoint = endPoint;
            _owner.AddPoint(controlPoint1);
            _owner.AddPoint(controlPoint2);
            _owner.AddPoint(endPoint);
            _owner._commands.Add(builder => builder.CubicBezierTo(controlPoint1, controlPoint2, endPoint));
        }

        public void QuadraticBezierTo(Point controlPoint, Point endPoint, bool isStroked = true)
        {
            _currentPoint = endPoint;
            _owner.AddPoint(controlPoint);
            _owner.AddPoint(endPoint);
            _owner._commands.Add(builder => builder.QuadraticBezierTo(controlPoint, endPoint));
        }

        public void EndFigure(bool isClosed) => _owner._commands.Add(builder => builder.EndFigure(isClosed));

        public void Dispose()
        {
        }
    }
}

internal sealed class CoreTextPathBuilder
{
    private readonly IntPtr _context;
    private readonly Stack<Matrix> _transforms = new();
    private Matrix _currentTransform = Matrix.Identity;

    public CoreTextPathBuilder(IntPtr context)
    {
        _context = context;
    }

    public FillRule FillRule { get; set; } = FillRule.NonZero;

    public void PushTransform(Matrix matrix)
    {
        _transforms.Push(_currentTransform);
        _currentTransform *= matrix;
    }

    public void PopTransform()
    {
        _currentTransform = _transforms.Pop();
    }

    public void BeginFigure(Point point)
    {
        var p = Transform(point);
        CoreTextNative.CGContextMoveToPoint(_context, p.X, p.Y);
    }

    public void LineTo(Point point)
    {
        var p = Transform(point);
        CoreTextNative.CGContextAddLineToPoint(_context, p.X, p.Y);
    }

    public void CubicBezierTo(Point controlPoint1, Point controlPoint2, Point endPoint)
    {
        var c1 = Transform(controlPoint1);
        var c2 = Transform(controlPoint2);
        var end = Transform(endPoint);
        CoreTextNative.CGContextAddCurveToPoint(_context, c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y);
    }

    public void QuadraticBezierTo(Point controlPoint, Point endPoint)
    {
        var current = Transform(controlPoint);
        var end = Transform(endPoint);
        CoreTextNative.CGContextAddQuadCurveToPoint(_context, current.X, current.Y, end.X, end.Y);
    }

    public void AddRect(Rect rect)
    {
        var r = Transform(rect);
        CoreTextNative.CGContextAddRect(_context, new CoreTextNative.CGRect(r.X, r.Y, r.Width, r.Height));
    }

    public void AddEllipse(Rect rect)
    {
        var r = Transform(rect);
        CoreTextNative.CGContextAddEllipseInRect(_context, new CoreTextNative.CGRect(r.X, r.Y, r.Width, r.Height));
    }

    public void AddNativePath(IntPtr path) => CoreTextNative.CGContextAddPath(_context, path);

    public void EndFigure(bool close)
    {
        if (close)
        {
            CoreTextNative.CGContextClosePath(_context);
        }
    }

    private Point Transform(Point point) => _currentTransform == Matrix.Identity ? point : point.Transform(_currentTransform);

    private Rect Transform(Rect rect)
    {
        if (_currentTransform == Matrix.Identity)
        {
            return rect;
        }

        return rect.TransformToAABB(_currentTransform);
    }
}
