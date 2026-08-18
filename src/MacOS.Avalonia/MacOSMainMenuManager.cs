using Avalonia.Reactive;
using System.Collections.Specialized;

namespace MacOS.Avalonia;

internal sealed class MacOSMainMenuManager : IDisposable
{
    private readonly NSApplication _application;
    private readonly MacOSPlatformOptions _options;
    private readonly MacOSNativeApplicationCommands _applicationCommands;
    private readonly NSMenu _detachedMainMenu = new();
    private readonly NSMenu _detachedServicesMenu = new();
    private readonly Dictionary<NSWindow, NativeMenu?> _windowMenus = new();
    private readonly List<Action> _applicationMenuDetachHandlers = new();
    private Application? _currentApplication;
    private IDisposable? _applicationMenuPropertySubscription;
    private NativeMenu? _applicationMenu;
    private NSWindow? _activeWindow;
    private NSMenu? _nativeMenu;
    private NSMenu? _servicesMenu;
    private bool _disposed;

    public MacOSMainMenuManager(NSApplication application, MacOSPlatformOptions options, MacOSNativeApplicationCommands applicationCommands)
    {
        _application = application;
        _options = options;
        _applicationCommands = applicationCommands;
        RefreshApplicationMenu();
        RebuildMainMenu();
    }

    public bool IsEnabled => !_options.DisableNativeMenus;

    public void SetWindowMenu(NSWindow window, NativeMenu? menu)
    {
        if (_disposed)
        {
            return;
        }

        if (menu is null)
        {
            _windowMenus.Remove(window);
        }
        else
        {
            _windowMenus[window] = menu;
            if (window.IsKeyWindow)
            {
                _activeWindow = window;
            }
        }

        RebuildMainMenu();
    }

    public void RemoveWindow(NSWindow window)
    {
        if (_disposed)
        {
            return;
        }

        _windowMenus.Remove(window);
        if (ReferenceEquals(_activeWindow, window))
        {
            _activeWindow = null;
        }

        RebuildMainMenu();
    }

    public void HandleWindowActivated(NSWindow window)
    {
        if (_disposed)
        {
            return;
        }

        _activeWindow = window;
        RefreshApplicationMenu();
        RebuildMainMenu();
    }

    public void HandleWindowDeactivated(NSWindow window)
    {
        if (_disposed)
        {
            return;
        }

        if (ReferenceEquals(_activeWindow, window))
        {
            _activeWindow = null;
            RebuildMainMenu();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _applicationMenuPropertySubscription?.Dispose();
        DetachApplicationMenu();
        ClearNativeMenu();
    }

    private void RefreshApplicationMenu()
    {
        var application = Application.Current;
        if (!ReferenceEquals(_currentApplication, application))
        {
            _applicationMenuPropertySubscription?.Dispose();
            _applicationMenuPropertySubscription = null;
            _currentApplication = application;

            if (_currentApplication is not null)
            {
                _applicationMenuPropertySubscription = _currentApplication
                    .GetObservable(NativeMenu.MenuProperty)
                    .Subscribe(new AnonymousObserver<NativeMenu?>(_ =>
                    {
                        UpdateApplicationMenuCore();
                        RebuildMainMenu();
                    }));
            }
        }

        UpdateApplicationMenuCore();
    }

    private void UpdateApplicationMenuCore()
    {
        DetachApplicationMenu();
        _applicationMenu = _currentApplication is not null
            ? NativeMenu.GetMenu(_currentApplication)
            : null;

        if (_applicationMenu is not null)
        {
            AttachApplicationMenu(_applicationMenu);
        }
    }

    private void AttachApplicationMenu(NativeMenu menu)
    {
        menu.NeedsUpdate += OnApplicationMenuChanged;
        _applicationMenuDetachHandlers.Add(() => menu.NeedsUpdate -= OnApplicationMenuChanged);

        if (menu.Items is INotifyCollectionChanged items)
        {
            items.CollectionChanged += OnApplicationMenuCollectionChanged;
            _applicationMenuDetachHandlers.Add(() => items.CollectionChanged -= OnApplicationMenuCollectionChanged);
        }

        foreach (var item in menu.Items)
        {
            AttachApplicationMenuItem(item);
        }
    }

    private void AttachApplicationMenuItem(NativeMenuItemBase item)
    {
        item.PropertyChanged += OnApplicationMenuItemPropertyChanged;
        _applicationMenuDetachHandlers.Add(() => item.PropertyChanged -= OnApplicationMenuItemPropertyChanged);

        if (item is NativeMenuItem menuItem && menuItem.Menu is { } submenu)
        {
            AttachApplicationMenu(submenu);
        }
    }

    private void DetachApplicationMenu()
    {
        for (var index = _applicationMenuDetachHandlers.Count - 1; index >= 0; index--)
        {
            _applicationMenuDetachHandlers[index]();
        }

        _applicationMenuDetachHandlers.Clear();
        _applicationMenu = null;
    }

    private void RebuildMainMenu()
    {
        if (_disposed || !IsEnabled)
        {
            return;
        }

        ClearNativeMenu();

        var mainMenu = new NSMenu();
        var hasItems = false;

        if (CreateApplicationRootItem() is { } applicationRoot)
        {
            mainMenu.AddItem(applicationRoot);
            hasItems = true;
        }

        if (_activeWindow is not null
            && _windowMenus.TryGetValue(_activeWindow, out var windowMenu)
            && windowMenu is { Items.Count: > 0 })
        {
            MacOSNativeMenuBuilder.AppendItems(mainMenu, windowMenu.Items);
            hasItems = true;
        }

        if (!hasItems)
        {
            mainMenu.Dispose();
            return;
        }

        _nativeMenu = mainMenu;
        _application.MainMenu = _nativeMenu;
    }

    private NSMenuItem? CreateApplicationRootItem()
    {
        var applicationMenu = _applicationMenu;
        var hasManagedItems = applicationMenu is { Items.Count: > 0 };
        var hasDefaultItems = !_options.DisableDefaultApplicationMenuItems;
        if (!hasManagedItems && !hasDefaultItems)
        {
            return null;
        }

        var submenu = applicationMenu is not null
            ? MacOSNativeMenuBuilder.CreateNativeMenu(applicationMenu)
            : new NSMenu();

        if (hasDefaultItems)
        {
            AppendDefaultApplicationItems(submenu, hasManagedItems);
        }

        var rootTitle = applicationMenu?.Parent is NativeMenuItem parentItem && !string.IsNullOrWhiteSpace(parentItem.Header)
            ? parentItem.Header!
            : Application.Current?.Name ?? "Application";

        return new NSMenuItem
        {
            Title = rootTitle,
            Submenu = submenu
        };
    }

    private void AppendDefaultApplicationItems(NSMenu submenu, bool prependSeparator)
    {
        if (prependSeparator)
        {
            submenu.AddItem(NSMenuItem.SeparatorItem);
        }

        var applicationName = Application.Current?.Name ?? "Application";

        var aboutItem = new NSMenuItem { Title = $"About {applicationName}" };
        aboutItem.Activated += (_, _) => ShowAboutDialog(applicationName);
        submenu.AddItem(aboutItem);

        submenu.AddItem(NSMenuItem.SeparatorItem);

        var servicesMenu = new NSMenu();
        _servicesMenu = servicesMenu;
        _application.ServicesMenu = servicesMenu;

        submenu.AddItem(new NSMenuItem
        {
            Title = "Services",
            Submenu = servicesMenu
        });

        submenu.AddItem(NSMenuItem.SeparatorItem);

        var hideItem = new NSMenuItem
        {
            Title = $"Hide {applicationName}",
            KeyEquivalent = "h",
            KeyEquivalentModifierMask = NSEventModifierMask.CommandKeyMask
        };
        hideItem.Activated += (_, _) => _applicationCommands.HideApp();
        submenu.AddItem(hideItem);

        var hideOthersItem = new NSMenuItem
        {
            Title = "Hide Others",
            KeyEquivalent = "h",
            KeyEquivalentModifierMask = NSEventModifierMask.CommandKeyMask | NSEventModifierMask.AlternateKeyMask
        };
        hideOthersItem.Activated += (_, _) => _applicationCommands.HideOthers();
        submenu.AddItem(hideOthersItem);

        var showAllItem = new NSMenuItem { Title = "Show All" };
        showAllItem.Activated += (_, _) => _applicationCommands.ShowAll();
        submenu.AddItem(showAllItem);

        submenu.AddItem(NSMenuItem.SeparatorItem);

        var quitItem = new NSMenuItem
        {
            Title = $"Quit {applicationName}",
            KeyEquivalent = "q",
            KeyEquivalentModifierMask = NSEventModifierMask.CommandKeyMask
        };
        quitItem.Activated += (_, _) => TryShutdownApplication();
        submenu.AddItem(quitItem);
    }

    private void ShowAboutDialog(string applicationName)
    {
        using var alert = new NSAlert
        {
            MessageText = $"About {applicationName}",
            InformativeText = CreateAboutText(applicationName)
        };

        alert.AddButton("OK");
        alert.RunModal();
    }

    private static string CreateAboutText(string applicationName)
    {
        var version = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleShortVersionString")?.ToString();
        var build = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleVersion")?.ToString();

        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(version))
        {
            details.Add($"Version {version}");
        }

        if (!string.IsNullOrWhiteSpace(build) && !string.Equals(build, version, StringComparison.Ordinal))
        {
            details.Add($"Build {build}");
        }

        return details.Count > 0
            ? string.Join(Environment.NewLine, details)
            : applicationName;
    }

    private void TryShutdownApplication()
    {
        if (Application.Current is { ApplicationLifetime: IClassicDesktopStyleApplicationLifetime lifetime })
        {
            lifetime.TryShutdown();
            return;
        }

        if (Application.Current is { ApplicationLifetime: IControlledApplicationLifetime controlledLifetime })
        {
            controlledLifetime.Shutdown();
        }
    }

    private void ClearNativeMenu()
    {
        if (ReferenceEquals(_application.ServicesMenu, _servicesMenu))
        {
            _application.ServicesMenu = _detachedServicesMenu;
        }

        _servicesMenu = null;

        if (ReferenceEquals(_application.MainMenu, _nativeMenu))
        {
            _application.MainMenu = _detachedMainMenu;
        }

        _nativeMenu?.Dispose();
        _nativeMenu = null;
    }

    private void OnApplicationMenuChanged(object? sender, EventArgs e)
    {
        RebuildMainMenu();
    }

    private void OnApplicationMenuCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildMainMenu();
    }

    private void OnApplicationMenuItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        RebuildMainMenu();
    }
}