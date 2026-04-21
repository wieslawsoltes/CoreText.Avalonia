using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace MacOS.Avalonia;

internal sealed class MacOSClipboardImpl : IClipboardImpl
{
    public Task<IAsyncDataTransfer?> TryGetDataAsync() => Task.FromResult<IAsyncDataTransfer?>(null);

    public Task SetDataAsync(IAsyncDataTransfer dataTransfer) => Task.CompletedTask;

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
