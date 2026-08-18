using Avalonia.Platform.Storage;

namespace CoreText.Avalonia.Tests;

public sealed class MacOSStorageProviderTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "CoreText.Avalonia.Tests", Guid.NewGuid().ToString("N"));

    public MacOSStorageProviderTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task TryGetFileFromPathAsync_ReturnsExistingFile()
    {
        var filePath = Path.Combine(_tempRoot, "note.txt");
        await File.WriteAllTextAsync(filePath, "hello");
        var provider = new MacOS.Avalonia.MacOSStorageProvider(null);

        var file = await provider.TryGetFileFromPathAsync(new Uri(filePath));

        Assert.NotNull(file);
        Assert.Equal("note.txt", file.Name);
    }

    [Fact]
    public async Task TryGetFolderFromPathAsync_ReturnsExistingFolder()
    {
        var folderPath = Path.Combine(_tempRoot, "docs");
        Directory.CreateDirectory(folderPath);
        var provider = new MacOS.Avalonia.MacOSStorageProvider(null);

        var folder = await provider.TryGetFolderFromPathAsync(new Uri(folderPath));

        Assert.NotNull(folder);
        Assert.Equal("docs", folder.Name);
    }

    [Fact]
    public async Task StorageFolder_CanCreateResolveAndEnumerateChildren()
    {
        var folder = new MacOS.Avalonia.MacOSStorageFolder(new DirectoryInfo(_tempRoot));

        var childFolder = await folder.CreateFolderAsync("assets");
        var childFile = await folder.CreateFileAsync("readme.txt");
        var resolvedFolder = await folder.GetFolderAsync("assets");
        var resolvedFile = await folder.GetFileAsync("readme.txt");

        Assert.NotNull(childFolder);
        Assert.NotNull(childFile);
        Assert.NotNull(resolvedFolder);
        Assert.NotNull(resolvedFile);

        var itemNames = new List<string>();
        await foreach (var item in folder.GetItemsAsync())
        {
            itemNames.Add(item.Name);
        }

        Assert.Contains("assets", itemNames);
        Assert.Contains("readme.txt", itemNames);
    }

    [Fact]
    public async Task StorageFile_SaveBookmarkAsync_RoundTripsThroughProvider()
    {
        var filePath = Path.Combine(_tempRoot, "bookmark.txt");
        await File.WriteAllTextAsync(filePath, "hello from bookmark");
        var provider = new MacOS.Avalonia.MacOSStorageProvider(null);

        var file = await provider.TryGetFileFromPathAsync(new Uri(filePath));

        Assert.NotNull(file);
        Assert.True(file.CanBookmark);

        var bookmark = await file.SaveBookmarkAsync();

        Assert.False(string.IsNullOrWhiteSpace(bookmark));

        var reopened = await provider.OpenFileBookmarkAsync(bookmark!);

        Assert.NotNull(reopened);
        Assert.Equal("bookmark.txt", reopened.Name);

        await using var stream = await reopened.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();

        Assert.Equal("hello from bookmark", text);
    }

    [Fact]
    public async Task StorageFolder_SaveBookmarkAsync_RoundTripsThroughProvider()
    {
        var folderPath = Path.Combine(_tempRoot, "bookmarked-folder");
        Directory.CreateDirectory(folderPath);
        await File.WriteAllTextAsync(Path.Combine(folderPath, "inside.txt"), "inside");
        var provider = new MacOS.Avalonia.MacOSStorageProvider(null);

        var folder = await provider.TryGetFolderFromPathAsync(new Uri(folderPath));

        Assert.NotNull(folder);
        Assert.True(folder.CanBookmark);

        var bookmark = await folder.SaveBookmarkAsync();

        Assert.False(string.IsNullOrWhiteSpace(bookmark));

        var reopened = await provider.OpenFolderBookmarkAsync(bookmark!);

        Assert.NotNull(reopened);
        Assert.Equal("bookmarked-folder", reopened.Name);
        Assert.NotNull(await reopened.GetFileAsync("inside.txt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}