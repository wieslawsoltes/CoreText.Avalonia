using System.IO;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace MacOS.Avalonia;

internal sealed class MacOSCursorFactory : ICursorFactory
{
    public ICursorImpl GetCursor(StandardCursorType cursorType)
    {
        return new MacOSCursorImpl(cursorType switch
        {
            StandardCursorType.Arrow => NSCursor.ArrowCursor,
            StandardCursorType.Ibeam => NSCursor.IBeamCursor,
            StandardCursorType.Cross => NSCursor.CrosshairCursor,
            StandardCursorType.Hand => NSCursor.PointingHandCursor,
            StandardCursorType.SizeWestEast => NSCursor.ResizeLeftRightCursor,
            StandardCursorType.SizeNorthSouth => NSCursor.ResizeUpDownCursor,
            StandardCursorType.SizeAll => NSCursor.OpenHandCursor,
            StandardCursorType.No => NSCursor.OperationNotAllowedCursor,
            _ => NSCursor.ArrowCursor
        });
    }

    public ICursorImpl CreateCursor(Bitmap cursor, PixelPoint hotSpot)
    {
        using var stream = new MemoryStream();
        cursor.Save(stream);
        using var data = NSData.FromArray(stream.ToArray());
        using var image = new NSImage(data);
        return new MacOSCursorImpl(new NSCursor(image, new CGPoint(hotSpot.X, hotSpot.Y)));
    }
}

internal sealed class MacOSCursorImpl(NSCursor cursor) : ICursorImpl
{
    public NSCursor Cursor { get; } = cursor;

    public void Dispose()
    {
        Cursor.Dispose();
    }
}
