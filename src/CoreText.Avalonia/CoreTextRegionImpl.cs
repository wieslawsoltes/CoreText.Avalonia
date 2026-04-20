using Avalonia.Platform;

namespace CoreText.Avalonia;

internal sealed class CoreTextRegionImpl : IPlatformRenderInterfaceRegion
{
    private readonly List<LtrbPixelRect> _rects = new();

    public void AddRect(LtrbPixelRect rect)
    {
        if (rect.Left == rect.Right || rect.Top == rect.Bottom)
        {
            return;
        }

        _rects.Add(rect);
    }

    public void Reset() => _rects.Clear();

    public bool IsEmpty => _rects.Count == 0;

    public LtrbPixelRect Bounds =>
        _rects.Count == 0
            ? default
            : _rects.Aggregate(static (left, right) => new LtrbPixelRect
            {
                Left = Math.Min(left.Left, right.Left),
                Top = Math.Min(left.Top, right.Top),
                Right = Math.Max(left.Right, right.Right),
                Bottom = Math.Max(left.Bottom, right.Bottom)
            });

    public IList<LtrbPixelRect> Rects => _rects;

    public bool Intersects(LtrbRect rect) => _rects.Any(x =>
        x.Left < rect.Right &&
        rect.Left < x.Right &&
        x.Top < rect.Bottom &&
        rect.Top < x.Bottom);

    public bool Contains(Point pt) => _rects.Any(x =>
        pt.X >= x.Left &&
        pt.X <= x.Right &&
        pt.Y >= x.Top &&
        pt.Y <= x.Bottom);

    public void Dispose()
    {
        _rects.Clear();
    }
}
