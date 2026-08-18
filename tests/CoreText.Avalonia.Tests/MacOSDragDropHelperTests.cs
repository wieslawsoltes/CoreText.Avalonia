using Avalonia.Input;

namespace CoreText.Avalonia.Tests;

public sealed class MacOSDragDropHelperTests
{
    [Fact]
    public void ToDragDropEffects_MapsSupportedNativeOperations()
    {
        var effects = MacOS.Avalonia.MacOSDragDropHelper.ToDragDropEffects(
            NSDragOperation.Copy | NSDragOperation.Move | NSDragOperation.Link);

        Assert.Equal(DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link, effects);
    }

    [Fact]
    public void ToNativeDragOperation_MapsSupportedAvaloniaEffects()
    {
        var operation = MacOS.Avalonia.MacOSDragDropHelper.ToNativeDragOperation(
            DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

        Assert.Equal(NSDragOperation.Copy | NSDragOperation.Move | NSDragOperation.Link, operation);
    }

    [Fact]
    public void RegisteredPasteboardTypes_IncludeCoreClipboardFormats()
    {
        Assert.Contains(MacOS.Avalonia.MacOSClipboardDataFormatHelper.Utf8PlainTextPasteboardType, MacOS.Avalonia.MacOSDragDropHelper.RegisteredPasteboardTypes);
        Assert.Contains(MacOS.Avalonia.MacOSClipboardDataFormatHelper.FileUrlPasteboardType, MacOS.Avalonia.MacOSDragDropHelper.RegisteredPasteboardTypes);
        Assert.Contains(MacOS.Avalonia.MacOSClipboardDataFormatHelper.PngPasteboardType, MacOS.Avalonia.MacOSDragDropHelper.RegisteredPasteboardTypes);
    }
}