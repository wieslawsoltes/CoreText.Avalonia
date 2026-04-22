using Avalonia.Input.Platform;
using Avalonia.OpenGL;
using Avalonia.Rendering;

namespace MacOS.Avalonia;

public sealed class MacOSPlatform : IWindowingPlatform
{
    private static MacOSPlatform? s_instance;
    private readonly List<MacOSWindowImpl> _windows = new();
    private readonly object _windowsSync = new();
    private NSObject? _screenParametersObserver;
    private MacOSMainMenuManager? _mainMenuManager;
    private MacOSDockMenuProvider? _dockMenuProvider;

    private MacOSPlatform(
        MacOSPlatformOptions options,
        NSApplication nativeApplication,
        MacOSPlatformLifetimeEvents lifetimeEvents,
        MacOSActivatableLifetime activatableLifetime,
        MacOSScreenImpl screens,
        IPlatformGraphics? platformGraphics)
    {
        Options = options;
        NativeApplication = nativeApplication;
        LifetimeEvents = lifetimeEvents;
        ActivatableLifetime = activatableLifetime;
        Screens = screens;
        PlatformGraphics = platformGraphics;
        KeyboardDevice = new KeyboardDevice();
    }

    public MacOSPlatformOptions Options { get; }

    internal NSApplication NativeApplication { get; }

    internal MacOSPlatformLifetimeEvents LifetimeEvents { get; }

    internal MacOSActivatableLifetime ActivatableLifetime { get; }

    internal MacOSScreenImpl Screens { get; }

    internal KeyboardDevice KeyboardDevice { get; }

    internal IPlatformGraphics? PlatformGraphics { get; }

    internal MacOSMainMenuManager MainMenuManager => _mainMenuManager ?? throw new InvalidOperationException("Main menu manager is not initialized.");

    internal static Compositor Compositor { get; private set; } = null!;

    public static MacOSPlatform Initialize(MacOSPlatformOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (s_instance is not null)
        {
            return s_instance;
        }

        NSApplication.Init();
        var nativeApplication = NSApplication.SharedApplication;
        nativeApplication.ActivationPolicy = options.ShowInDock
            ? NSApplicationActivationPolicy.Regular
            : NSApplicationActivationPolicy.Accessory;

        var lifetimeEvents = new MacOSPlatformLifetimeEvents();
        var activatableLifetime = new MacOSActivatableLifetime(nativeApplication);
        var screens = new MacOSScreenImpl();
        var platformGraphics = options.CustomPlatformGraphics ?? TryCreatePlatformGraphics(options);

        s_instance = new MacOSPlatform(
            options,
            nativeApplication,
            lifetimeEvents,
            activatableLifetime,
            screens,
            platformGraphics);
        s_instance.InitializeBindings();
        return s_instance;
    }

    public IWindowImpl CreateWindow() => new MacOSWindowImpl(this, Options);

    public IWindowImpl CreateEmbeddableWindow() => new MacOSEmbeddableWindowImpl(this);

    public ITopLevelImpl CreateEmbeddableTopLevel() => new MacOSEmbeddableWindowImpl(this);

    public ITrayIconImpl? CreateTrayIcon() => new MacOSTrayIconImpl();

    public void GetWindowsZOrder(ReadOnlySpan<IWindowImpl> windows, Span<long> zOrder)
    {
        lock (_windowsSync)
        {
            for (var index = 0; index < windows.Length; index++)
            {
                zOrder[index] = 0;
                if (windows[index] is not MacOSWindowImpl target)
                {
                    continue;
                }

                var order = _windows.IndexOf(target);
                zOrder[index] = order < 0 ? 0 : order + 1;
            }
        }
    }

    internal void RegisterWindow(MacOSWindowImpl window)
    {
        lock (_windowsSync)
        {
            _windows.Add(window);
        }
    }

    internal void UnregisterWindow(MacOSWindowImpl window)
    {
        lock (_windowsSync)
        {
            _windows.Remove(window);
        }
    }

    private void InitializeBindings()
    {
        Dispatcher.InitializeUIThreadDispatcher(new MacOSDispatcherImpl(NativeApplication));

        var clipboardImpl = new MacOSClipboardImpl();
        var clipboard = new MacOSClipboard(clipboardImpl);
        var applicationCommands = new MacOSNativeApplicationCommands(NativeApplication);
        _mainMenuManager = new MacOSMainMenuManager(NativeApplication, Options, applicationCommands);
        _dockMenuProvider = new MacOSDockMenuProvider(Options);
        var hotkeys = new PlatformHotkeyConfiguration(KeyModifiers.Meta, wholeWordTextActionModifiers: KeyModifiers.Alt);
        hotkeys.MoveCursorToTheStartOfLine.Add(new KeyGesture(Key.Left, hotkeys.CommandModifiers));
        hotkeys.MoveCursorToTheStartOfLineWithSelection.Add(new KeyGesture(Key.Left, hotkeys.CommandModifiers | hotkeys.SelectionModifiers));
        hotkeys.MoveCursorToTheEndOfLine.Add(new KeyGesture(Key.Right, hotkeys.CommandModifiers));
        hotkeys.MoveCursorToTheEndOfLineWithSelection.Add(new KeyGesture(Key.Right, hotkeys.CommandModifiers | hotkeys.SelectionModifiers));
        var keyGestureFormatInfo = new KeyGestureFormatInfo(
            new Dictionary<Key, string>
            {
                { Key.Back, "⌫" },
                { Key.Down, "↓" },
                { Key.End, "↘" },
                { Key.Escape, "⎋" },
                { Key.Home, "↖" },
                { Key.Left, "←" },
                { Key.Return, "↩" },
                { Key.PageDown, "⇟" },
                { Key.PageUp, "⇞" },
                { Key.Right, "→" },
                { Key.Space, "␣" },
                { Key.Tab, "⇥" },
                { Key.Up, "↑" }
            },
            ctrl: "⌃",
            meta: "⌘",
            shift: "⇧",
            alt: "⌥");

        if (!Options.DisableAvaloniaAppDelegate)
        {
            NativeApplication.Delegate = new MacOSApplicationDelegate(LifetimeEvents, ActivatableLifetime, _dockMenuProvider);
        }

        AvaloniaLocator.CurrentMutable
            .Bind<IClipboardImpl>().ToConstant(clipboardImpl)
            .Bind<IClipboard>().ToConstant(clipboard)
            .Bind<ICursorFactory>().ToConstant(new MacOSCursorFactory())
            .Bind<IPlatformDragSource>().ToSingleton<MacOSDragSource>()
            .Bind<IMountedVolumeInfoProvider>().ToConstant(new MacOSMountedVolumeInfoProvider())
            .Bind<IScreenImpl>().ToConstant(Screens)
            .Bind<IPlatformSettings>().ToConstant(new MacOSPlatformSettings())
            .Bind<IPlatformIconLoader>().ToSingleton<MacOSIconLoader>()
            .Bind<IKeyboardDevice>().ToConstant(KeyboardDevice)
            .Bind<IWindowingPlatform>().ToConstant(this)
            .Bind<IRenderLoop>().ToConstant(RenderLoop.FromTimer(new DefaultRenderTimer(60)))
            .Bind<PlatformHotkeyConfiguration>().ToConstant(hotkeys)
            .Bind<KeyGestureFormatInfo>().ToConstant(keyGestureFormatInfo)
            .Bind<IPlatformLifetimeEventsImpl>().ToConstant(LifetimeEvents)
            .Bind<IActivatableLifetime>().ToConstant(ActivatableLifetime);

        if (PlatformGraphics is not null)
        {
            AvaloniaLocator.CurrentMutable.Bind<IPlatformGraphics>().ToConstant(PlatformGraphics);
            if (PlatformGraphics is IPlatformGraphicsOpenGlContextFactory openGlFactory)
            {
                AvaloniaLocator.CurrentMutable.Bind<IPlatformGraphicsOpenGlContextFactory>().ToConstant(openGlFactory);
            }
        }

        Compositor = new Compositor(PlatformGraphics, true);
        AvaloniaLocator.CurrentMutable.Bind<Compositor>().ToConstant(Compositor);

        _screenParametersObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            new NSString("NSApplicationDidChangeScreenParametersNotification"),
            _ => Dispatcher.UIThread.Post(Screens.NotifyChanged, DispatcherPriority.Input));

        NativeApplication.FinishLaunching();
    }

    private static IPlatformGraphics? TryCreatePlatformGraphics(MacOSPlatformOptions options)
    {
        foreach (var renderingMode in options.RenderingModes)
        {
            switch (renderingMode)
            {
                case MacOSRenderingMode.Metal:
                {
                    var device = MTLDevice.SystemDefault;
                    if (device is not null)
                    {
                        return new MacOSMetalPlatformGraphics(device);
                    }

                    break;
                }
                case MacOSRenderingMode.OpenGl:
                    return new MacOSOpenGlPlatformGraphics();
                case MacOSRenderingMode.Software:
                    return null;
            }
        }

        return null;
    }
}