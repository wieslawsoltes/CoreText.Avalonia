using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace MacOS.Avalonia;

internal sealed class MacOSWindowMenuExporter : ITopLevelNativeMenuExporter, IDisposable
{
    private readonly MacOSMainMenuManager _mainMenuManager;
    private readonly NSWindow _window;
    private readonly List<Action> _detachHandlers = new();
    private NativeMenu? _menu;
    private bool _disposed;
    private bool _isNativeMenuExported;

    public MacOSWindowMenuExporter(MacOSMainMenuManager mainMenuManager, NSWindow window)
    {
        _mainMenuManager = mainMenuManager;
        _window = window;
    }

    public bool IsNativeMenuExported => _isNativeMenuExported;

    public event EventHandler? OnIsNativeMenuExportedChanged;

    public void SetNativeMenu(NativeMenu? menu)
    {
        if (_disposed)
        {
            return;
        }

        if (ReferenceEquals(_menu, menu))
        {
            NotifyMenuChanged();
            return;
        }

        DetachManagedMenu();
        _menu = menu;
        if (_menu is not null)
        {
            AttachManagedMenu(_menu);
        }

        NotifyMenuChanged();
    }

    public void HandleActivated()
    {
        if (_disposed)
        {
            return;
        }

        _mainMenuManager.HandleWindowActivated(_window);
        UpdateExported(_mainMenuManager.IsEnabled && _menu is not null);
    }

    public void HandleDeactivated()
    {
        _mainMenuManager.HandleWindowDeactivated(_window);
        UpdateExported(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DetachManagedMenu();
        _mainMenuManager.RemoveWindow(_window);
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

    private void NotifyMenuChanged()
    {
        if (_disposed)
        {
            return;
        }

        _mainMenuManager.SetWindowMenu(_window, _menu);
        UpdateExported(_mainMenuManager.IsEnabled && _window.IsKeyWindow && _menu is not null);
    }

    private void UpdateExported(bool isExported)
    {
        if (_isNativeMenuExported == isExported)
        {
            return;
        }

        _isNativeMenuExported = isExported;
        OnIsNativeMenuExportedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMenuChanged(object? sender, EventArgs e)
    {
        NotifyMenuChanged();
    }

    private void OnMenuCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyMenuChanged();
    }

    private void OnMenuItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        NotifyMenuChanged();
    }
}