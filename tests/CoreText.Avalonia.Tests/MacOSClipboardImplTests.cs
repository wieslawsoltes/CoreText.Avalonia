using Avalonia.Input;

namespace CoreText.Avalonia.Tests;

public sealed class MacOSClipboardImplTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "CoreText.Avalonia.Tests", Guid.NewGuid().ToString("N"));

    public MacOSClipboardImplTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task ExtractPasteboardPayloadAsync_ReturnsTextAndFileUrl()
    {
        var filePath = Path.Combine(_tempRoot, "clipboard sample.txt");
        await File.WriteAllTextAsync(filePath, "hello");
        var transfer = new DataTransfer();
        var item = new DataTransferItem();
        item.SetText("sample text");
        item.SetFile(new MacOS.Avalonia.MacOSStorageFile(new FileInfo(filePath)));
        transfer.Add(item);

        var payload = await MacOS.Avalonia.MacOSClipboardImpl.ExtractPasteboardPayloadAsync(transfer);

        Assert.Equal("sample text", payload.Text);
        Assert.Equal(new Uri(filePath, UriKind.Absolute).AbsoluteUri, payload.FileUrl);
        Assert.Null(payload.BitmapPngBytes);
        Assert.Empty(payload.StringValues);
        Assert.Empty(payload.ByteValues);
    }

    [Fact]
    public void TryCreateStorageItem_ReturnsExistingFolderForFileUrl()
    {
        var folderPath = Path.Combine(_tempRoot, "folder with spaces");
        Directory.CreateDirectory(folderPath);

        var item = MacOS.Avalonia.MacOSClipboardImpl.TryCreateStorageItem(new Uri(folderPath, UriKind.Absolute).AbsoluteUri);

        Assert.NotNull(item);
        Assert.Equal("folder with spaces", item.Name);
    }

    [Fact]
    public void TryCreateStorageItem_ReturnsNullForNonFileUrl()
    {
        var item = MacOS.Avalonia.MacOSClipboardImpl.TryCreateStorageItem("https://example.com/test.txt");

        Assert.Null(item);
    }

    [Fact]
    public void TryCreateBitmap_ReturnsNullForInvalidPngBytes()
    {
        using var invalidDecode = MacOS.Avalonia.MacOSClipboardImpl.TryCreateBitmap([1, 2, 3, 4]);

        Assert.Null(invalidDecode);
        Assert.Null(MacOS.Avalonia.MacOSClipboardImpl.TryCreateBitmapPngBytes(null));
    }

    [Fact]
    public async Task ExtractPasteboardPayloadAsync_ReturnsPlatformStringAndByteFormats()
    {
        var transfer = new DataTransfer();
        var item = new DataTransferItem();
        var htmlFormat = DataFormat.CreateStringPlatformFormat("public.html");
        var bytesFormat = DataFormat.CreateBytesPlatformFormat("com.example.binary");
        item.Set(htmlFormat, "<b>sample</b>");
        item.Set(bytesFormat, [1, 2, 3, 4]);
        transfer.Add(item);

        var payload = await MacOS.Avalonia.MacOSClipboardImpl.ExtractPasteboardPayloadAsync(transfer);

        Assert.Null(payload.Text);
        Assert.Null(payload.FileUrl);
        Assert.Null(payload.BitmapPngBytes);
        Assert.Equal("<b>sample</b>", payload.StringValues["public.html"]);
        Assert.Equal([1, 2, 3, 4], payload.ByteValues["com.example.binary"]);
    }

    [Fact]
    public async Task ExtractPasteboardPayload_ReturnsSynchronousDragPayload()
    {
        var filePath = Path.Combine(_tempRoot, "drag sample.txt");
        await File.WriteAllTextAsync(filePath, "drag");
        var transfer = new DataTransfer();
        var item = new DataTransferItem();
        var htmlFormat = DataFormat.CreateStringPlatformFormat("public.html");
        var bytesFormat = DataFormat.CreateBytesPlatformFormat("com.example.binary");
        item.SetText("drag text");
        item.SetFile(new MacOS.Avalonia.MacOSStorageFile(new FileInfo(filePath)));
        item.Set(htmlFormat, "<i>drag</i>");
        item.Set(bytesFormat, [9, 8, 7]);
        transfer.Add(item);

        var payload = MacOS.Avalonia.MacOSClipboardImpl.ExtractPasteboardPayload(transfer);

        Assert.Equal("drag text", payload.Text);
        Assert.Equal(new Uri(filePath, UriKind.Absolute).AbsoluteUri, payload.FileUrl);
        Assert.Null(payload.BitmapPngBytes);
        Assert.Equal("<i>drag</i>", payload.StringValues["public.html"]);
        Assert.Equal([9, 8, 7], payload.ByteValues["com.example.binary"]);
    }

    [Fact]
    public void ClipboardDataFormatHelper_MapsHtmlAndBinaryFormats()
    {
        Assert.Equal(DataFormat.Text, MacOS.Avalonia.MacOSClipboardDataFormatHelper.ToDataFormat(MacOS.Avalonia.MacOSClipboardDataFormatHelper.Utf8PlainTextPasteboardType));
        Assert.Equal(DataFormat.File, MacOS.Avalonia.MacOSClipboardDataFormatHelper.ToDataFormat(MacOS.Avalonia.MacOSClipboardDataFormatHelper.FileUrlPasteboardType));
        Assert.Equal(DataFormat.Bitmap, MacOS.Avalonia.MacOSClipboardDataFormatHelper.ToDataFormat(MacOS.Avalonia.MacOSClipboardDataFormatHelper.PngPasteboardType));
        Assert.Equal(DataFormat.CreateStringPlatformFormat("public.html"), MacOS.Avalonia.MacOSClipboardDataFormatHelper.ToDataFormat("public.html"));
        Assert.Equal(DataFormat.CreateBytesPlatformFormat("com.example.binary"), MacOS.Avalonia.MacOSClipboardDataFormatHelper.ToDataFormat("com.example.binary"));
        Assert.Equal("public.html", MacOS.Avalonia.MacOSClipboardDataFormatHelper.ToSystemType(DataFormat.CreateStringPlatformFormat("public.html")));
        Assert.Equal("com.example.binary", MacOS.Avalonia.MacOSClipboardDataFormatHelper.ToSystemType(DataFormat.CreateBytesPlatformFormat("com.example.binary")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}