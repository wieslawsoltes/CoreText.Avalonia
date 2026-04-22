using System.Threading.Tasks;
using Avalonia.VisualTree;

namespace MacOS.Avalonia;

internal sealed class MacOSDragSource : IPlatformDragSource
{
    public async Task<DragDropEffects> DoDragDropAsync(
        PointerPressedEventArgs triggerEvent,
        IDataTransfer dataTransfer,
        DragDropEffects allowedEffects)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(triggerEvent);
        ArgumentNullException.ThrowIfNull(dataTransfer);

        var topLevel = TopLevel.GetTopLevel(triggerEvent.Source as Visual);
        if (topLevel?.PlatformImpl is not MacOSTopLevelImpl topLevelImpl)
        {
            throw new ArgumentException("Drag and drop requires a macOS top-level implementation.", nameof(triggerEvent));
        }

        var currentEvent = NSApplication.SharedApplication.CurrentEvent;
        if (currentEvent is null)
        {
            return DragDropEffects.None;
        }

        var payload = MacOSClipboardImpl.ExtractPasteboardPayload(dataTransfer);
        using var pasteboardItem = CreatePasteboardItem(payload);
        if (pasteboardItem is null)
        {
            return DragDropEffects.None;
        }

        using var dragImage = CreateDragImage(payload);
        using var draggingSource = new MacOSDraggingSource(allowedEffects);

        triggerEvent.Pointer.Capture(null);
        topLevelImpl.BeginDraggingSession(
            pasteboardItem,
            triggerEvent.GetPosition(topLevel),
            currentEvent,
            draggingSource,
            dragImage);

        return await draggingSource.Completion.ConfigureAwait(true);
    }

    private static NSPasteboardItem? CreatePasteboardItem(MacOSClipboardPayload payload)
    {
        var pasteboardItem = new NSPasteboardItem();
        var hasPayload = false;

        if (!string.IsNullOrEmpty(payload.Text))
        {
            pasteboardItem.SetStringForType(payload.Text, MacOSClipboardDataFormatHelper.Utf8PlainTextPasteboardType);
            hasPayload = true;
        }

        if (!string.IsNullOrEmpty(payload.FileUrl))
        {
            pasteboardItem.SetStringForType(payload.FileUrl, MacOSClipboardDataFormatHelper.FileUrlPasteboardType);
            if (MacOSClipboardImpl.TryGetPasteboardFileDisplayName(payload.FileUrl) is { Length: > 0 } displayName)
            {
                pasteboardItem.SetStringForType(displayName, MacOSClipboardDataFormatHelper.UrlNamePasteboardType);
            }

            hasPayload = true;
        }

        if (payload.BitmapPngBytes is { Length: > 0 })
        {
            using var data = NSData.FromArray(payload.BitmapPngBytes);
            pasteboardItem.SetDataForType(data, MacOSClipboardDataFormatHelper.PngPasteboardType);
            hasPayload = true;
        }

        foreach (var stringValue in payload.StringValues)
        {
            pasteboardItem.SetStringForType(stringValue.Value, stringValue.Key);
            hasPayload = true;
        }

        foreach (var byteValue in payload.ByteValues)
        {
            using var data = NSData.FromArray(byteValue.Value);
            pasteboardItem.SetDataForType(data, byteValue.Key);
            hasPayload = true;
        }

        if (hasPayload)
        {
            return pasteboardItem;
        }

        pasteboardItem.Dispose();
        return null;
    }

    private static NSImage CreateDragImage(MacOSClipboardPayload payload)
    {
        var descriptor = CreatePreviewDescriptor(payload);
        var image = new NSImage(new CGSize(220, 52));
        image.LockFocus();

        var backgroundRect = new CGRect(0, 0, 220, 52);
        var contentRect = new CGRect(1, 1, 218, 50);
        NSColor.FromRgba(22, 44, 67, 42).SetFill();
        NSBezierPath.FillRect(backgroundRect);
        NSColor.FromRgb(250, 252, 253).SetFill();
        NSBezierPath.FillRect(contentRect);

        using var icon = CreatePreviewIcon(descriptor.FileType, descriptor.FilePath);
        icon.Size = new CGSize(28, 28);
        icon.Draw(new CGRect(12, 12, 28, 28));

        var titleAttributes = new NSStringAttributes
        {
            Font = NSFont.BoldSystemFontOfSize(13),
            ForegroundColor = NSColor.FromRgb(23, 50, 77)
        };
        var subtitleAttributes = new NSStringAttributes
        {
            Font = NSFont.SystemFontOfSize(11),
            ForegroundColor = NSColor.FromRgba(92, 112, 131, 220)
        };

        NSStringDrawing.DrawInRect(descriptor.Title, new CGRect(50, 10, 156, 16), titleAttributes);
        NSStringDrawing.DrawInRect(descriptor.Subtitle, new CGRect(50, 26, 156, 14), subtitleAttributes);
        image.UnlockFocus();
        return image;
    }

    private static DragPreviewDescriptor CreatePreviewDescriptor(MacOSClipboardPayload payload)
    {
        if (payload.FileUrl is { Length: > 0 } fileUrl && Uri.TryCreate(fileUrl, UriKind.Absolute, out var fileUri) && fileUri.IsFile)
        {
            var fileName = Path.GetFileName(fileUri.LocalPath);
            var extension = Path.GetExtension(fileUri.LocalPath).TrimStart('.');
            return new DragPreviewDescriptor(
                string.IsNullOrWhiteSpace(fileName) ? "Dragged file" : fileName,
                "Drop in Finder or another file-aware app",
                string.IsNullOrWhiteSpace(extension) ? "txt" : extension,
                fileUri.LocalPath);
        }

        if (payload.Text is { Length: > 0 } text)
        {
            var singleLine = text.ReplaceLineEndings(" ").Trim();
            if (singleLine.Length > 34)
            {
                singleLine = singleLine[..31] + "...";
            }

            return new DragPreviewDescriptor(
                string.IsNullOrWhiteSpace(singleLine) ? "Dragged text" : singleLine,
                "Drop in TextEdit, Notes, or the sample target",
                null,
                null);
        }

        if (payload.StringValues.Count > 0 || payload.ByteValues.Count > 0)
        {
            return new DragPreviewDescriptor(
                "Custom payload",
                "App-specific formats are included in this drag",
                null,
                null);
        }

        return new DragPreviewDescriptor("Drag payload", "Move to a compatible drop target", null, null);
    }

    private static NSImage CreatePreviewIcon(string? fileType, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return NSWorkspace.SharedWorkspace.IconForFile(filePath);
        }

        if (!string.IsNullOrWhiteSpace(fileType))
        {
            return NSWorkspace.SharedWorkspace.IconForFileType(fileType);
        }

        var image = new NSImage(new CGSize(28, 28));
        image.LockFocus();
        NSColor.FromRgb(14, 116, 144).SetFill();
        NSBezierPath.FillRect(new CGRect(0, 0, 28, 28));
        image.UnlockFocus();
        return image;
    }

    private readonly record struct DragPreviewDescriptor(string Title, string Subtitle, string? FileType, string? FilePath);

    private sealed class MacOSDraggingSource(DragDropEffects allowedEffects) : NSDraggingSource
    {
        private readonly DragDropEffects _allowedEffects = allowedEffects;
        private readonly NSDragOperation _allowedOperations = MacOSDragDropHelper.ToNativeDragOperation(allowedEffects);
        private readonly TaskCompletionSource<DragDropEffects> _completion = new();

        public Task<DragDropEffects> Completion => _completion.Task;

        public override NSDragOperation DraggingSourceOperationMaskForLocal(bool isLocal)
        {
            return _allowedOperations;
        }

        public override void DraggedImageEndedAtOperation(NSImage image, CGPoint screenPoint, NSDragOperation operation)
        {
            var completedEffect = MacOSDragDropHelper.ToDragDropEffects(operation) & _allowedEffects;
            _completion.TrySetResult(completedEffect);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _completion.TrySetResult(DragDropEffects.None);
            }

            base.Dispose(disposing);
        }
    }
}