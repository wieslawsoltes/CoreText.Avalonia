using System.Collections.ObjectModel;
using Avalonia.Controls.Platform;
using Avalonia.Reactive;

namespace MacOS.Avalonia;

internal sealed class MacOSMountedVolumeInfoProvider : IMountedVolumeInfoProvider
{
    public IDisposable Listen(ObservableCollection<MountedVolumeInfo> mountedDrives)
    {
        return new MacOSMountedVolumeInfoListener(mountedDrives);
    }

    internal static MountedVolumeInfo[] EnumerateMountedVolumes(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        return Directory.GetDirectories(rootPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(static path => new MountedVolumeInfo
            {
                VolumeLabel = Path.GetFileName(path),
                VolumePath = path,
                VolumeSizeBytes = 0
            })
            .ToArray();
    }
}

internal sealed class MacOSMountedVolumeInfoListener : IDisposable
{
    private readonly ObservableCollection<MountedVolumeInfo> _mountedDrives;
    private readonly IDisposable _subscription;

    public MacOSMountedVolumeInfoListener(ObservableCollection<MountedVolumeInfo> mountedDrives)
    {
        _mountedDrives = mountedDrives;
        _subscription = DispatcherTimer.Run(Poll, TimeSpan.FromSeconds(1));
        Poll();
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }

    private bool Poll()
    {
        var mountVolumes = MacOSMountedVolumeInfoProvider.EnumerateMountedVolumes("/Volumes");
        if (_mountedDrives.SequenceEqual(mountVolumes))
        {
            return true;
        }

        _mountedDrives.Clear();
        foreach (var volume in mountVolumes)
        {
            _mountedDrives.Add(volume);
        }

        return true;
    }
}