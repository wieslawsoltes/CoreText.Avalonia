using System.IO;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Avalonia.Platform.Storage;

namespace MacOS.Avalonia;

internal sealed class MacOSStorageProvider(MacOSTopLevelImpl? owner) : IStorageProvider
{
    internal static ReadOnlySpan<byte> PlatformKey => "macOS"u8;

    public bool CanOpen => true;

    public bool CanSave => true;

    public bool CanPickFolder => true;

    public async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            using var panel = NSOpenPanel.OpenPanel;
            ConfigureCommonOptions(panel, options);
            panel.CanChooseFiles = true;
            panel.CanChooseDirectories = false;
            panel.AllowsMultipleSelection = options.AllowMultiple;
            ApplyFileTypes(panel, options.FileTypeFilter, options.SuggestedFileType);

            if (!RunPanel(panel))
            {
                return (IReadOnlyList<IStorageFile>)Array.Empty<IStorageFile>();
            }

            return panel.Urls?
                .Select(CreateFileFromUrl)
                .Where(static file => file is not null)
                .Cast<IStorageFile>()
                .ToArray()
                ?? Array.Empty<IStorageFile>();
        });
    }

    public async Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
    {
        var result = await SaveFilePickerWithResultAsync(options).ConfigureAwait(false);
        return result.File;
    }

    public async Task<SaveFilePickerResult> SaveFilePickerWithResultAsync(FilePickerSaveOptions options)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            using var panel = new NSSavePanel();
            ConfigureCommonOptions(panel, options);
            panel.CanCreateDirectories = true;
            panel.ExtensionHidden = false;
            panel.NameFieldStringValue = ApplyDefaultExtension(options.SuggestedFileName, options.DefaultExtension);
            ApplyFileTypes(panel, options.FileTypeChoices, options.SuggestedFileType);

            if (!RunPanel(panel))
            {
                return new SaveFilePickerResult();
            }

            var file = CreateFileFromUrl(panel.Url);
            return new SaveFilePickerResult
            {
                File = file,
                SelectedFileType = MatchSelectedFileType(options.FileTypeChoices, file)
            };
        });
    }

    public async Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            using var panel = NSOpenPanel.OpenPanel;
            ConfigureCommonOptions(panel, options);
            panel.CanChooseFiles = false;
            panel.CanChooseDirectories = true;
            panel.CanCreateDirectories = true;
            panel.AllowsMultipleSelection = options.AllowMultiple;

            if (!RunPanel(panel))
            {
                return (IReadOnlyList<IStorageFolder>)Array.Empty<IStorageFolder>();
            }

            return panel.Urls?
                .Select(CreateFolderFromUrl)
                .Where(static folder => folder is not null)
                .Cast<IStorageFolder>()
                .ToArray()
                ?? Array.Empty<IStorageFolder>();
        });
    }

    public Task<IStorageBookmarkFile?> OpenFileBookmarkAsync(string bookmark)
    {
        return Task.FromResult(ReadBookmarkItem(bookmark) as IStorageBookmarkFile);
    }

    public Task<IStorageBookmarkFolder?> OpenFolderBookmarkAsync(string bookmark)
    {
        return Task.FromResult(ReadBookmarkItem(bookmark) as IStorageBookmarkFolder);
    }

    public Task<IStorageFile?> TryGetFileFromPathAsync(Uri filePath)
    {
        if (TryGetLocalPath(filePath) is { } path && File.Exists(path))
        {
            return Task.FromResult<IStorageFile?>(new MacOSStorageFile(new FileInfo(path)));
        }

        return Task.FromResult<IStorageFile?>(null);
    }

    public Task<IStorageFolder?> TryGetFolderFromPathAsync(Uri folderPath)
    {
        if (TryGetLocalPath(folderPath) is { } path && Directory.Exists(path))
        {
            return Task.FromResult<IStorageFolder?>(new MacOSStorageFolder(new DirectoryInfo(path)));
        }

        return Task.FromResult<IStorageFolder?>(null);
    }

    public Task<IStorageFolder?> TryGetWellKnownFolderAsync(WellKnownFolder wellKnownFolder)
    {
        var folderPath = wellKnownFolder switch
        {
            WellKnownFolder.Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            WellKnownFolder.Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            WellKnownFolder.Downloads => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            WellKnownFolder.Music => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            WellKnownFolder.Pictures => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            WellKnownFolder.Videos => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            _ => null
        };

        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
        {
            return Task.FromResult<IStorageFolder?>(new MacOSStorageFolder(new DirectoryInfo(folderPath)));
        }

        return Task.FromResult<IStorageFolder?>(null);
    }

    private void ConfigureCommonOptions(NSSavePanel panel, PickerOptions options)
    {
        panel.Title = options.Title ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(options.SuggestedFileName))
        {
            panel.NameFieldStringValue = options.SuggestedFileName;
        }

        if (options.SuggestedStartLocation?.TryGetLocalPath() is { Length: > 0 } path)
        {
            panel.DirectoryUrl = new NSUrl(path, true);
        }
    }

    private void ApplyFileTypes(NSSavePanel panel, IReadOnlyList<FilePickerFileType>? fileTypes, FilePickerFileType? suggestedFileType)
    {
        var selected = suggestedFileType is null
            ? fileTypes
            : fileTypes?.Where(type => ReferenceEquals(type, suggestedFileType)).Concat(fileTypes.Where(type => !ReferenceEquals(type, suggestedFileType))).ToArray();

        var allowedTypes = selected?
            .SelectMany(GetAllowedTypeIdentifiers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (allowedTypes is { Length: > 0 })
        {
            panel.AllowedFileTypes = allowedTypes;
        }
    }

    private static IEnumerable<string> GetAllowedTypeIdentifiers(FilePickerFileType type)
    {
        if (type.AppleUniformTypeIdentifiers is { Count: > 0 })
        {
            return type.AppleUniformTypeIdentifiers;
        }

        if (type.Patterns is not { Count: > 0 })
        {
            return Array.Empty<string>();
        }

        return type.Patterns
            .Select(Path.GetExtension)
            .Where(static extension => !string.IsNullOrEmpty(extension) && extension[0] == '.' && extension.IndexOf('*') < 0)
            .Select(static extension => extension![1..]);
    }

    private static string ApplyDefaultExtension(string? fileName, string? defaultExtension)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(defaultExtension) || Path.HasExtension(fileName))
        {
            return fileName;
        }

        return fileName + (defaultExtension.StartsWith('.') ? defaultExtension : "." + defaultExtension);
    }

    private static FilePickerFileType? MatchSelectedFileType(IReadOnlyList<FilePickerFileType>? fileTypes, IStorageFile? file)
    {
        if (fileTypes is null || file is null)
        {
            return null;
        }

        var extension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var normalizedExtension = extension.TrimStart('.');
        return fileTypes.FirstOrDefault(type =>
            GetAllowedTypeIdentifiers(type).Any(identifier => string.Equals(identifier, normalizedExtension, StringComparison.OrdinalIgnoreCase)));
    }

    private bool RunPanel(NSSavePanel panel)
    {
        var ownerWindow = owner?.DialogOwnerWindow;
        if (ownerWindow is null)
        {
            return panel.RunModal() == 1;
        }

        var completion = new TaskCompletionSource<nint>();
        panel.BeginSheet(ownerWindow, response => completion.SetResult(response));
        return completion.Task.GetAwaiter().GetResult() == 1;
    }

    private static MacOSStorageFile? CreateFileFromUrl(NSUrl? url)
    {
        return CreateItemFromUrl(url, url) as MacOSStorageFile;
    }

    private static MacOSStorageFolder? CreateFolderFromUrl(NSUrl? url)
    {
        return CreateItemFromUrl(url, url) as MacOSStorageFolder;
    }

    private static IStorageBookmarkItem? ReadBookmarkItem(string bookmark)
    {
        if (MacOSStorageBookmarkHelper.TryDecodeBookmark(PlatformKey, bookmark, out var bytes) == MacOSStorageBookmarkHelper.DecodeResult.Success)
        {
            using var data = NSData.FromArray(bytes!);
            if (data is null)
            {
                return null;
            }

            var url = NSUrl.FromBookmarkData(
                data,
                NSUrlBookmarkResolutionOptions.WithSecurityScope | NSUrlBookmarkResolutionOptions.WithoutUI,
                null,
                out _,
                out var error);

            if (error is null && CreateItemFromUrl(url, url) is IStorageBookmarkItem item)
            {
                return item;
            }
        }

        if (MacOSStorageBookmarkHelper.TryDecodeBclBookmark(bookmark, out var path))
        {
            return CreateItemFromPath(path) as IStorageBookmarkItem;
        }

        return null;
    }

    private static IStorageItem? CreateItemFromPath(string path, NSUrl? scopeOwnerUrl = null)
    {
        if (Directory.Exists(path))
        {
            return new MacOSStorageFolder(new DirectoryInfo(path), null, scopeOwnerUrl);
        }

        if (File.Exists(path))
        {
            return new MacOSStorageFile(new FileInfo(path), null, scopeOwnerUrl);
        }

        return null;
    }

    private static IStorageItem? CreateItemFromUrl(NSUrl? url, NSUrl? scopeOwnerUrl = null)
    {
        if (url?.Path is not { Length: > 0 } path)
        {
            return null;
        }

        if (Directory.Exists(path))
        {
            return new MacOSStorageFolder(new DirectoryInfo(path), url, scopeOwnerUrl ?? url);
        }

        if (File.Exists(path))
        {
            return new MacOSStorageFile(new FileInfo(path), url, scopeOwnerUrl ?? url);
        }

        return null;
    }

    private static string? TryGetLocalPath(Uri uri)
    {
        if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.LocalPath;
    }
}

internal abstract class MacOSStorageItem<TInfo> : IStorageBookmarkItem where TInfo : FileSystemInfo
{
    protected MacOSStorageItem(TInfo fileSystemInfo, NSUrl? url = null, NSUrl? scopeOwnerUrl = null)
    {
        FileSystemInfo = fileSystemInfo ?? throw new ArgumentNullException(nameof(fileSystemInfo));
        Url = url;
        ScopeOwnerUrl = scopeOwnerUrl;
    }

    protected TInfo FileSystemInfo { get; }
    internal NSUrl? Url { get; }
    internal NSUrl? ScopeOwnerUrl { get; }

    public string Name => FileSystemInfo.Name;

    public Uri Path => new(FileSystemInfo.FullName, UriKind.Absolute);

    public bool CanBookmark => true;

    public Task<StorageItemProperties> GetBasicPropertiesAsync()
    {
        using var scope = OpenScope();
        var size = FileSystemInfo is FileInfo fileInfo && fileInfo.Exists ? (ulong)fileInfo.Length : 0UL;
        return Task.FromResult(new StorageItemProperties(size, FileSystemInfo.CreationTimeUtc, FileSystemInfo.LastWriteTimeUtc));
    }

    public Task<string?> SaveBookmarkAsync()
    {
        if (Url is null)
        {
            return Task.FromResult<string?>(MacOSStorageBookmarkHelper.EncodeBclBookmark(FileSystemInfo.FullName));
        }

        using var scope = OpenScope();
        using var bookmarkData = Url.CreateBookmarkData(NSUrlBookmarkCreationOptions.WithSecurityScope, Array.Empty<string>(), null, out var error);
        if (bookmarkData is null || error is not null)
        {
            return Task.FromResult<string?>(MacOSStorageBookmarkHelper.EncodeBclBookmark(FileSystemInfo.FullName));
        }

        return Task.FromResult(MacOSStorageBookmarkHelper.EncodeBookmark(MacOSStorageProvider.PlatformKey, bookmarkData.ToArray()));
    }

    public Task ReleaseBookmarkAsync()
    {
        return Task.CompletedTask;
    }

    public Task<IStorageFolder?> GetParentAsync()
    {
        using var scope = OpenScope();
        var parent = FileSystemInfo switch
        {
            FileInfo { Directory: not null } fileInfo => fileInfo.Directory,
            DirectoryInfo { Parent: not null } directoryInfo => directoryInfo.Parent,
            _ => null
        };

        return Task.FromResult(parent is null ? null : new MacOSStorageFolder(parent, null, ScopeOwnerUrl) as IStorageFolder);
    }

    public Task DeleteAsync()
    {
        using var scope = OpenScope();
        if (FileSystemInfo is DirectoryInfo directoryInfo)
        {
            directoryInfo.Delete(true);
        }
        else
        {
            FileSystemInfo.Delete();
        }

        return Task.CompletedTask;
    }

    public Task<IStorageItem?> MoveAsync(IStorageFolder destination)
    {
        using var scope = OpenScope();
        using var destinationScope = (destination as MacOSStorageFolder)?.OpenScope();
        if (destination.TryGetLocalPath() is not { Length: > 0 } destinationPath)
        {
            return Task.FromResult<IStorageItem?>(null);
        }

        var newPath = System.IO.Path.Combine(destinationPath, FileSystemInfo.Name);
        var destinationOwnerUrl = (destination as MacOSStorageFolder)?.ScopeOwnerUrl;
        if (FileSystemInfo is DirectoryInfo directoryInfo)
        {
            directoryInfo.MoveTo(newPath);
            return Task.FromResult<IStorageItem?>(new MacOSStorageFolder(new DirectoryInfo(newPath), null, destinationOwnerUrl));
        }

        if (FileSystemInfo is FileInfo fileInfo)
        {
            fileInfo.MoveTo(newPath);
            return Task.FromResult<IStorageItem?>(new MacOSStorageFile(new FileInfo(newPath), null, destinationOwnerUrl));
        }

        return Task.FromResult<IStorageItem?>(null);
    }

    internal IDisposable? OpenScope()
    {
        return ScopeOwnerUrl is null ? null : MacOSSecurityScopeManager.Open(ScopeOwnerUrl);
    }

    public void Dispose()
    {
    }
}

internal sealed class MacOSStorageFile(FileInfo fileInfo, NSUrl? url = null, NSUrl? scopeOwnerUrl = null)
    : MacOSStorageItem<FileInfo>(fileInfo, url, scopeOwnerUrl), IStorageBookmarkFile
{
    public Task<Stream> OpenReadAsync()
    {
        var scope = OpenScope();
        var stream = FileSystemInfo.OpenRead();
        return Task.FromResult<Stream>(scope is null ? stream : new MacOSSecurityScopedStream(stream, scope));
    }

    public Task<Stream> OpenWriteAsync()
    {
        var scope = OpenScope();
        var stream = new FileStream(FileSystemInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.Write);
        return Task.FromResult<Stream>(scope is null ? stream : new MacOSSecurityScopedStream(stream, scope));
    }
}

internal sealed class MacOSStorageFolder(DirectoryInfo directoryInfo, NSUrl? url = null, NSUrl? scopeOwnerUrl = null)
    : MacOSStorageItem<DirectoryInfo>(directoryInfo, url, scopeOwnerUrl), IStorageBookmarkFolder
{
    public async IAsyncEnumerable<IStorageItem> GetItemsAsync()
    {
        using var scope = OpenScope();

        foreach (var directory in FileSystemInfo.EnumerateDirectories())
        {
            yield return new MacOSStorageFolder(directory, null, ScopeOwnerUrl);
            await Task.Yield();
        }

        foreach (var file in FileSystemInfo.EnumerateFiles())
        {
            yield return new MacOSStorageFile(file, null, ScopeOwnerUrl);
            await Task.Yield();
        }
    }

    public Task<IStorageFolder?> GetFolderAsync(string name)
    {
        using var scope = OpenScope();
        var path = System.IO.Path.Combine(FileSystemInfo.FullName, name);
        return Task.FromResult(Directory.Exists(path) ? new MacOSStorageFolder(new DirectoryInfo(path), null, ScopeOwnerUrl) as IStorageFolder : null);
    }

    public Task<IStorageFile?> GetFileAsync(string name)
    {
        using var scope = OpenScope();
        var path = System.IO.Path.Combine(FileSystemInfo.FullName, name);
        return Task.FromResult(File.Exists(path) ? new MacOSStorageFile(new FileInfo(path), null, ScopeOwnerUrl) as IStorageFile : null);
    }

    public Task<IStorageFile?> CreateFileAsync(string name)
    {
        using var scope = OpenScope();
        var path = System.IO.Path.Combine(FileSystemInfo.FullName, name);
        using var stream = File.Create(path);
        return Task.FromResult<IStorageFile?>(new MacOSStorageFile(new FileInfo(path), null, ScopeOwnerUrl));
    }

    public Task<IStorageFolder?> CreateFolderAsync(string name)
    {
        using var scope = OpenScope();
        var created = Directory.CreateDirectory(System.IO.Path.Combine(FileSystemInfo.FullName, name));
        return Task.FromResult<IStorageFolder?>(new MacOSStorageFolder(created, null, ScopeOwnerUrl));
    }
}

internal sealed class MacOSSecurityScopedStream(Stream inner, IDisposable scope) : Stream
{
    private readonly Stream _inner = inner;
    private readonly IDisposable _scope = scope;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _inner.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _scope.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        _scope.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}

internal static class MacOSSecurityScopeManager
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, int> UseCounts = new(StringComparer.Ordinal);

    public static IDisposable? Open(NSUrl url)
    {
        var key = url.AbsoluteString ?? url.Path ?? string.Empty;
        var shouldStart = false;

        lock (Sync)
        {
            if (UseCounts.TryGetValue(key, out var count))
            {
                UseCounts[key] = count + 1;
            }
            else
            {
                UseCounts[key] = 1;
                shouldStart = true;
            }
        }

        if (shouldStart && !url.StartAccessingSecurityScopedResource())
        {
            lock (Sync)
            {
                UseCounts.Remove(key);
            }

            return null;
        }

        return new ScopeLease(url, key);
    }

    private static void Close(NSUrl url, string key)
    {
        var shouldStop = false;

        lock (Sync)
        {
            if (!UseCounts.TryGetValue(key, out var count))
            {
                return;
            }

            if (count == 1)
            {
                UseCounts.Remove(key);
                shouldStop = true;
            }
            else
            {
                UseCounts[key] = count - 1;
            }
        }

        if (shouldStop)
        {
            url.StopAccessingSecurityScopedResource();
        }
    }

    private sealed class ScopeLease(NSUrl url, string key) : IDisposable
    {
        private NSUrl? _url = url;
        private string? _key = key;

        public void Dispose()
        {
            if (_url is not null && _key is not null)
            {
                Close(_url, _key);
                _url = null;
                _key = null;
            }
        }
    }
}

internal static class MacOSStorageBookmarkHelper
{
    private const int HeaderLength = 16;
    private static ReadOnlySpan<byte> AvaHeaderPrefix => "ava.v1."u8;
    private static ReadOnlySpan<byte> FakeBclBookmarkPlatform => "bcl"u8;

    public enum DecodeResult
    {
        Success = 0,
        InvalidFormat,
        InvalidPlatform
    }

    public static string? EncodeBookmark(ReadOnlySpan<byte> platform, ReadOnlySpan<byte> nativeBookmarkBytes)
    {
        if (nativeBookmarkBytes.Length == 0)
        {
            return null;
        }

        if (platform.Length == 0 || platform.Length > HeaderLength)
        {
            throw new ArgumentException($"Platform name should not be longer than {HeaderLength} bytes", nameof(platform));
        }

        var length = HeaderLength + nativeBookmarkBytes.Length;
        var rented = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            var span = rented.AsSpan(0, length);
            span.Clear();
            AvaHeaderPrefix.CopyTo(span);
            platform.CopyTo(span.Slice(AvaHeaderPrefix.Length));
            nativeBookmarkBytes.CopyTo(span.Slice(HeaderLength));
            return Convert.ToBase64String(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public static bool TryDecodeBclBookmark(string bookmark, [NotNullWhen(true)] out string? localPath)
    {
        var decodeResult = TryDecodeBookmark(FakeBclBookmarkPlatform, bookmark, out var bytes);
        if (decodeResult == DecodeResult.Success)
        {
            localPath = Encoding.UTF8.GetString(bytes!);
            return true;
        }

        if (decodeResult == DecodeResult.InvalidFormat
            && bookmark.IndexOfAny(Path.GetInvalidPathChars()) < 0
            && !string.IsNullOrEmpty(System.IO.Path.GetDirectoryName(bookmark)))
        {
            localPath = bookmark;
            return true;
        }

        localPath = null;
        return false;
    }

    public static string EncodeBclBookmark(string localPath)
    {
        return EncodeBookmark(FakeBclBookmarkPlatform, Encoding.UTF8.GetBytes(localPath))!;
    }

    public static DecodeResult TryDecodeBookmark(ReadOnlySpan<byte> platform, string? base64Bookmark, out byte[]? nativeBookmark)
    {
        if (platform.Length == 0
            || platform.Length > HeaderLength
            || base64Bookmark is null
            || base64Bookmark.Length % 4 != 0)
        {
            nativeBookmark = null;
            return DecodeResult.InvalidFormat;
        }

        var rented = ArrayPool<byte>.Shared.Rent(HeaderLength + base64Bookmark.Length * 6);
        try
        {
            if (!Convert.TryFromBase64Chars(base64Bookmark, rented, out var bytesWritten))
            {
                nativeBookmark = null;
                return DecodeResult.InvalidFormat;
            }

            var decoded = rented.AsSpan(0, bytesWritten);
            if (decoded.Length < HeaderLength || !AvaHeaderPrefix.SequenceEqual(decoded.Slice(0, AvaHeaderPrefix.Length)))
            {
                nativeBookmark = null;
                return DecodeResult.InvalidFormat;
            }

            if (!decoded.Slice(AvaHeaderPrefix.Length, platform.Length).SequenceEqual(platform))
            {
                nativeBookmark = null;
                return DecodeResult.InvalidPlatform;
            }

            nativeBookmark = decoded.Slice(HeaderLength).ToArray();
            return DecodeResult.Success;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}