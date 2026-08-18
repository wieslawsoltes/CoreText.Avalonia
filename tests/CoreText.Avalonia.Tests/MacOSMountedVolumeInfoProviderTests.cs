using Avalonia.Controls.Platform;

namespace CoreText.Avalonia.Tests;

public sealed class MacOSMountedVolumeInfoProviderTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "CoreText.Avalonia.Tests.Volumes", Guid.NewGuid().ToString("N"));

    public MacOSMountedVolumeInfoProviderTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void EnumerateMountedVolumes_ReturnsDirectoryBackedEntries()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "DriveA"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "DriveB"));

        var volumes = MacOS.Avalonia.MacOSMountedVolumeInfoProvider.EnumerateMountedVolumes(_tempRoot);

        Assert.Equal(2, volumes.Length);
        Assert.Collection(volumes,
            first =>
            {
                Assert.Equal("DriveA", first.VolumeLabel);
                Assert.Equal(Path.Combine(_tempRoot, "DriveA"), first.VolumePath);
                Assert.Equal(0UL, first.VolumeSizeBytes);
            },
            second =>
            {
                Assert.Equal("DriveB", second.VolumeLabel);
                Assert.Equal(Path.Combine(_tempRoot, "DriveB"), second.VolumePath);
                Assert.Equal(0UL, second.VolumeSizeBytes);
            });
    }

    [Fact]
    public void EnumerateMountedVolumes_ReturnsEmptyArray_ForMissingRoot()
    {
        var volumes = MacOS.Avalonia.MacOSMountedVolumeInfoProvider.EnumerateMountedVolumes(Path.Combine(_tempRoot, "missing"));

        Assert.Empty(volumes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}