namespace MacOS.Avalonia;

internal static class MacOSDragDropHelper
{
    public static readonly string[] RegisteredPasteboardTypes =
    [
        MacOSClipboardDataFormatHelper.Utf8PlainTextPasteboardType,
        MacOSClipboardDataFormatHelper.FileUrlPasteboardType,
        MacOSClipboardDataFormatHelper.PngPasteboardType,
        "public.html",
        "public.rtf",
        "public.xml",
        "public.json",
        MacOSClipboardDataFormatHelper.UrlNamePasteboardType
    ];

    public static DragDropEffects ToDragDropEffects(NSDragOperation operation)
    {
        var effects = DragDropEffects.None;

        if (operation.HasFlag(NSDragOperation.Copy))
        {
            effects |= DragDropEffects.Copy;
        }

        if (operation.HasFlag(NSDragOperation.Move))
        {
            effects |= DragDropEffects.Move;
        }

        if (operation.HasFlag(NSDragOperation.Link))
        {
            effects |= DragDropEffects.Link;
        }

        return effects;
    }

    public static NSDragOperation ToNativeDragOperation(DragDropEffects effects)
    {
        var operation = NSDragOperation.None;

        if (effects.HasFlag(DragDropEffects.Copy))
        {
            operation |= NSDragOperation.Copy;
        }

        if (effects.HasFlag(DragDropEffects.Move))
        {
            operation |= NSDragOperation.Move;
        }

        if (effects.HasFlag(DragDropEffects.Link))
        {
            operation |= NSDragOperation.Link;
        }

        return operation;
    }
}