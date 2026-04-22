using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace MacOS.Avalonia;

internal sealed class MacOSTrayMenuExporter : INativeMenuExporter, IDisposable
{
    private readonly NSStatusItem _statusItem;
    private readonly List<Action> _detachHandlers = new();
    private NativeMenu? _menu;
    private NSMenu? _nativeMenu;
    private bool _disposed;

    public MacOSTrayMenuExporter(NSStatusItem statusItem)
    {
        _statusItem = statusItem;
    }

    public void SetNativeMenu(NativeMenu? menu)
    {
        if (_disposed)
        {
            return;
        }

        if (ReferenceEquals(_menu, menu))
        {
            RebuildNativeMenu();
            return;
        }

        DetachManagedMenu();
        _menu = menu;
        if (_menu is null)
        {
            ClearNativeMenu();
            return;
        }

        AttachManagedMenu(_menu);
        RebuildNativeMenu();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DetachManagedMenu();
        ClearNativeMenu();
    }

    private void AttachManagedMenu(NativeMenu menu)
    {
        menu.NeedsUpdate += OnMenuChanged;
        _detachHandlers.Add(() => menu.NeedsUpdate -= OnMenuChanged);

        if (menu.Items is INotifyCollectionChanged items)
        {
            items.CollectionChanged += OnMenuCollectionChanged;
            _detachHandlers.Add(() => items.CollectionChanged -= OnMenuCollectionChanged);
        }

        foreach (var item in menu.Items)
        {
            AttachManagedItem(item);
        }
    }

    private void AttachManagedItem(NativeMenuItemBase item)
    {
        item.PropertyChanged += OnMenuItemPropertyChanged;
        _detachHandlers.Add(() => item.PropertyChanged -= OnMenuItemPropertyChanged);

        if (item is NativeMenuItem menuItem && menuItem.Menu is { } submenu)
        {
            AttachManagedMenu(submenu);
        }
    }

    private void DetachManagedMenu()
    {
        for (var index = _detachHandlers.Count - 1; index >= 0; index--)
        {
            _detachHandlers[index]();
        }

        _detachHandlers.Clear();
        _menu = null;
    }

    private void RebuildNativeMenu()
    {
        if (_disposed)
        {
            return;
        }

        ClearNativeMenu();
        if (_menu is null)
        {
            return;
        }

        _nativeMenu = MacOSNativeMenuBuilder.CreateNativeMenu(_menu);
        _statusItem.Menu = _nativeMenu;
    }

    private void ClearNativeMenu()
    {
        _statusItem.Menu = null;
        _nativeMenu?.Dispose();
        _nativeMenu = null;
    }

    private void OnMenuChanged(object? sender, EventArgs e)
    {
        RebuildNativeMenu();
    }

    private void OnMenuCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildNativeMenu();
    }

    private void OnMenuItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        RebuildNativeMenu();
    }
}