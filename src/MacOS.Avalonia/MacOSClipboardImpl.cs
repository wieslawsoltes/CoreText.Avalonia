using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace MacOS.Avalonia;

internal sealed class MacOSClipboardImpl : IClipboardImpl
{
    public Task<IAsyncDataTransfer?> TryGetDataAsync()
    {
        return Task.FromResult<IAsyncDataTransfer?>(TryCreateDataTransfer(NSPasteboard.GeneralPasteboard));
    }

    public async Task SetDataAsync(IAsyncDataTransfer dataTransfer)
    {
        var pasteboard = NSPasteboard.GeneralPasteboard;
        pasteboard.ClearContents();

        var payload = await ExtractPasteboardPayloadAsync(dataTransfer);
        if (!string.IsNullOrEmpty(payload.Text))
        {
            pasteboard.SetStringForType(payload.Text, MacOSClipboardDataFormatHelper.Utf8PlainTextPasteboardType);
        }

        if (!string.IsNullOrEmpty(payload.FileUrl))
        {
            pasteboard.SetStringForType(payload.FileUrl, MacOSClipboardDataFormatHelper.FileUrlPasteboardType);
            if (TryGetPasteboardFileDisplayName(payload.FileUrl) is { Length: > 0 } displayName)
            {
                pasteboard.SetStringForType(displayName, MacOSClipboardDataFormatHelper.UrlNamePasteboardType);
            }
        }

        if (payload.BitmapPngBytes is { Length: > 0 })
        {
            using var bitmapData = NSData.FromArray(payload.BitmapPngBytes);
            pasteboard.SetDataForType(bitmapData, MacOSClipboardDataFormatHelper.PngPasteboardType);
        }

        foreach (var stringValue in payload.StringValues)
        {
            pasteboard.SetStringForType(stringValue.Value, stringValue.Key);
        }

        foreach (var byteValue in payload.ByteValues)
        {
            using var data = NSData.FromArray(byteValue.Value);
            pasteboard.SetDataForType(data, byteValue.Key);
        }
    }

    public Task ClearAsync()
    {
        NSPasteboard.GeneralPasteboard.ClearContents();
        return Task.CompletedTask;
    }

    internal static async Task<MacOSClipboardPayload> ExtractPasteboardPayloadAsync(IAsyncDataTransfer dataTransfer)
    {
        string? text = null;
        string? fileUrl = null;
        byte[]? bitmapPngBytes = null;
        var stringValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byteValues = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in dataTransfer.Items)
        {
            foreach (var format in item.Formats)
            {
                if (text is null && DataFormat.Text.Equals(format))
                {
                    text = await item.TryGetTextAsync();
                    continue;
                }

                if (fileUrl is null && DataFormat.File.Equals(format))
                {
                    fileUrl = TryGetPasteboardFileUrl(await item.TryGetFileAsync());
                    continue;
                }

                if (bitmapPngBytes is null && DataFormat.Bitmap.Equals(format))
                {
                    bitmapPngBytes = TryCreateBitmapPngBytes(await item.TryGetBitmapAsync());
                    continue;
                }

                var systemType = MacOSClipboardDataFormatHelper.ToSystemType(format);
                switch (format)
                {
                    case DataFormat<string> stringFormat:
                    {
                        if (stringValues.ContainsKey(systemType))
                        {
                            break;
                        }

                        var stringValue = await item.TryGetValueAsync(stringFormat);
                        if (!string.IsNullOrEmpty(stringValue))
                        {
                            stringValues[systemType] = stringValue;
                        }

                        break;
                    }
                    case DataFormat<byte[]> bytesFormat:
                    {
                        if (byteValues.ContainsKey(systemType))
                        {
                            break;
                        }

                        var byteValue = await item.TryGetValueAsync(bytesFormat);
                        if (byteValue is { Length: > 0 })
                        {
                            byteValues[systemType] = byteValue;
                        }

                        break;
                    }
                }
            }
        }

        return new MacOSClipboardPayload(text, fileUrl, bitmapPngBytes, stringValues, byteValues);
    }

    internal static MacOSClipboardPayload ExtractPasteboardPayload(IDataTransfer dataTransfer)
    {
        string? text = null;
        string? fileUrl = null;
        byte[]? bitmapPngBytes = null;
        var stringValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byteValues = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in dataTransfer.Items)
        {
            foreach (var format in item.Formats)
            {
                if (text is null && DataFormat.Text.Equals(format))
                {
                    text = item.TryGetText();
                    continue;
                }

                if (fileUrl is null && DataFormat.File.Equals(format))
                {
                    fileUrl = TryGetPasteboardFileUrl(item.TryGetFile());
                    continue;
                }

                if (bitmapPngBytes is null && DataFormat.Bitmap.Equals(format))
                {
                    bitmapPngBytes = TryCreateBitmapPngBytes(item.TryGetBitmap());
                    continue;
                }

                var systemType = MacOSClipboardDataFormatHelper.ToSystemType(format);
                switch (format)
                {
                    case DataFormat<string> stringFormat:
                    {
                        if (stringValues.ContainsKey(systemType))
                        {
                            break;
                        }

                        var stringValue = item.TryGetValue(stringFormat);
                        if (!string.IsNullOrEmpty(stringValue))
                        {
                            stringValues[systemType] = stringValue;
                        }

                        break;
                    }
                    case DataFormat<byte[]> bytesFormat:
                    {
                        if (byteValues.ContainsKey(systemType))
                        {
                            break;
                        }

                        var byteValue = item.TryGetValue(bytesFormat);
                        if (byteValue is { Length: > 0 })
                        {
                            byteValues[systemType] = byteValue;
                        }

                        break;
                    }
                }
            }
        }

        return new MacOSClipboardPayload(text, fileUrl, bitmapPngBytes, stringValues, byteValues);
    }

    internal static IStorageItem? TryCreateStorageItem(string? fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            return null;
        }

        var localPath = uri.LocalPath;
        if (File.Exists(localPath))
        {
            return new MacOSStorageFile(new FileInfo(localPath));
        }

        if (Directory.Exists(localPath))
        {
            return new MacOSStorageFolder(new DirectoryInfo(localPath));
        }

        return null;
    }

    internal static string? TryGetPasteboardFileUrl(IStorageItem? storageItem)
    {
        if (storageItem?.TryGetLocalPath() is not { Length: > 0 } localPath)
        {
            return null;
        }

        return new Uri(localPath, UriKind.Absolute).AbsoluteUri;
    }

    internal static string? TryGetPasteboardFileDisplayName(string? fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            return null;
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    internal static byte[]? TryCreateBitmapPngBytes(Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return null;
        }

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream);
        return memoryStream.ToArray();
    }

    internal static Bitmap? TryCreateBitmap(byte[]? pngBytes)
    {
        if (pngBytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return new Bitmap(new MemoryStream(pngBytes, writable: false));
        }
        catch
        {
            return null;
        }
    }

    internal static DataTransfer? TryCreateDataTransfer(NSPasteboard? pasteboard)
    {
        if (pasteboard is null)
        {
            return null;
        }

        var availableTypes = pasteboard.Types;
        if (availableTypes is null || availableTypes.Length == 0)
        {
            return null;
        }

        var dataTransfer = new DataTransfer();
        var item = new DataTransferItem();

        foreach (var availableType in availableTypes)
        {
            AddPasteboardType(item, pasteboard, availableType?.ToString());
        }

        if (item.Formats.Count == 0)
        {
            return null;
        }

        dataTransfer.Add(item);
        return dataTransfer;
    }

    private static void AddPasteboardType(DataTransferItem item, NSPasteboard pasteboard, string? systemType)
    {
        if (string.IsNullOrWhiteSpace(systemType))
        {
            return;
        }

        var format = MacOSClipboardDataFormatHelper.ToDataFormat(systemType);
        if (DataFormat.Text.Equals(format))
        {
            var text = pasteboard.GetStringForType(systemType);
            if (!string.IsNullOrEmpty(text))
            {
                item.SetText(text);
            }

            return;
        }

        if (DataFormat.File.Equals(format))
        {
            var fileUrl = pasteboard.GetStringForType(systemType);
            if (TryCreateStorageItem(fileUrl) is { } storageItem)
            {
                item.SetFile(storageItem);
            }

            return;
        }

        if (DataFormat.Bitmap.Equals(format))
        {
            var bitmapData = pasteboard.GetDataForType(systemType);
            if (TryCreateBitmap(bitmapData?.ToArray()) is { } bitmap)
            {
                item.SetBitmap(bitmap);
            }

            return;
        }

        if (format is DataFormat<string> stringFormat)
        {
            var stringValue = pasteboard.GetStringForType(systemType);
            if (!string.IsNullOrEmpty(stringValue))
            {
                item.Set(stringFormat, stringValue);
            }

            return;
        }

        if (format is DataFormat<byte[]> bytesFormat)
        {
            var data = pasteboard.GetDataForType(systemType)?.ToArray();
            if (data is { Length: > 0 })
            {
                item.Set(bytesFormat, data);
            }
        }
    }
}

internal sealed record MacOSClipboardPayload(
    string? Text,
    string? FileUrl,
    byte[]? BitmapPngBytes,
    IReadOnlyDictionary<string, string> StringValues,
    IReadOnlyDictionary<string, byte[]> ByteValues);

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
