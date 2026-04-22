using System.IO;
using Avalonia.Controls.Platform;

namespace MacOS.Avalonia;

internal sealed class MacOSTrayIconImpl : ITrayIconWithIsTemplateImpl
{
    private readonly NSStatusItem _statusItem;
    private readonly MacOSTrayMenuExporter _menuExporter;
    private bool _disposed;
    private bool _isTemplateIcon;
    private bool _isVisible;
    private NSImage? _currentImage;

    public MacOSTrayIconImpl()
    {
        _statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
        _menuExporter = new MacOSTrayMenuExporter(_statusItem);
        _statusItem.Visible = false;
        if (_statusItem.Button is { } button)
        {
            button.Activated += OnButtonActivated;
        }
    }

    public Action? OnClicked { get; set; }

    public INativeMenuExporter? MenuExporter => _menuExporter;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_statusItem.Button is { } button)
        {
            button.Activated -= OnButtonActivated;
        }

        _menuExporter.Dispose();
        _currentImage?.Dispose();
        _currentImage = null;
        NSStatusBar.SystemStatusBar.RemoveStatusItem(_statusItem);
        _statusItem.Dispose();
    }

    public void SetIcon(IWindowIconImpl? icon)
    {
        _currentImage?.Dispose();
        _currentImage = CreateImage(icon);
        if (_currentImage is not null)
        {
            _currentImage.Template = _isTemplateIcon;
        }

        if (_statusItem.Button is { } button)
        {
            button.Image = _currentImage;
        }
    }

    public void SetToolTipText(string? text)
    {
        if (_statusItem.Button is { } button)
        {
            button.ToolTip = text;
        }
    }

    public void SetIsVisible(bool visible)
    {
        _isVisible = visible;
        _statusItem.Visible = visible;
    }

    public void SetIsTemplateIcon(bool isTemplateIcon)
    {
        _isTemplateIcon = isTemplateIcon;
        if (_currentImage is not null)
        {
            _currentImage.Template = isTemplateIcon;
        }
    }

    private void OnButtonActivated(object? sender, EventArgs e)
    {
        if (_isVisible && _statusItem.Menu is null)
        {
            OnClicked?.Invoke();
        }
    }

    private static NSImage? CreateImage(IWindowIconImpl? icon)
    {
        if (icon is null)
        {
            return null;
        }

        using var memoryStream = new MemoryStream();
        icon.Save(memoryStream);
        memoryStream.Position = 0;
        using var data = NSData.FromStream(memoryStream);
        if (data is null)
        {
            return null;
        }

        return new NSImage(data);
    }
}