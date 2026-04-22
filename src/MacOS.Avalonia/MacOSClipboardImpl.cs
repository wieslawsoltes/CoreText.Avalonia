using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace MacOS.Avalonia;

internal sealed class MacOSClipboardImpl : IClipboardImpl
{
    public Task<IAsyncDataTransfer?> TryGetDataAsync()
    {
        var pasteboard = NSPasteboard.GeneralPasteboard;
        var availableTypes = pasteboard.Types;
        if (availableTypes is null || availableTypes.Length == 0)
        {
            return Task.FromResult<IAsyncDataTransfer?>(null);
        }

        var dataTransfer = new DataTransfer();
        var item = new DataTransferItem();

        var text = pasteboard.GetStringForType(NSPasteboard.NSPasteboardTypeString);
        if (!string.IsNullOrEmpty(text))
        {
            item.SetText(text);
        }

        if (item.Formats.Count == 0)
        {
            return Task.FromResult<IAsyncDataTransfer?>(null);
        }

        dataTransfer.Add(item);
        return Task.FromResult<IAsyncDataTransfer?>(dataTransfer);
    }

    public async Task SetDataAsync(IAsyncDataTransfer dataTransfer)
    {
        var pasteboard = NSPasteboard.GeneralPasteboard;
        pasteboard.ClearContents();

        foreach (var item in dataTransfer.Items)
        {
            if (!item.Formats.Contains(DataFormat.Text))
            {
                continue;
            }

            var text = await item.TryGetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                pasteboard.SetStringForType(text, NSPasteboard.NSPasteboardTypeString);
                return;
            }
        }
    }

    public Task ClearAsync()
    {
        NSPasteboard.GeneralPasteboard.ClearContents();
        return Task.CompletedTask;
    }
}

internal sealed class MacOSClipboard(IClipboardImpl clipboardImpl) : IClipboard
{
    private readonly IClipboardImpl _clipboardImpl = clipboardImpl;
    private IAsyncDataTransfer? _lastDataTransfer;

    public Task ClearAsync()
    {
        _lastDataTransfer?.Dispose();
        _lastDataTransfer = null;
        return _clipboardImpl.ClearAsync();
    }

    public Task SetDataAsync(IAsyncDataTransfer? dataTransfer)
    {
        if (dataTransfer is null)
        {
            return ClearAsync();
        }

        _lastDataTransfer = dataTransfer;
        return _clipboardImpl.SetDataAsync(dataTransfer);
    }

    public Task FlushAsync() => Task.CompletedTask;

    public Task<IAsyncDataTransfer?> TryGetDataAsync() => _clipboardImpl.TryGetDataAsync();

    public Task<IAsyncDataTransfer?> TryGetInProcessDataAsync() => Task.FromResult(_lastDataTransfer);
}
