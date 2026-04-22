using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace MacOS.Avalonia;

internal static class MacOSNativeMenuBuilder
{
    public static NSMenu CreateNativeMenu(NativeMenu menu)
    {
        var nativeMenu = new NSMenu();
        AppendItems(nativeMenu, menu.Items);
        return nativeMenu;
    }

    public static void AppendItems(NSMenu nativeMenu, IEnumerable<NativeMenuItemBase> items)
    {
        foreach (var item in items)
        {
            if (CreateNativeMenuItem(item) is { } nativeItem)
            {
                nativeMenu.AddItem(nativeItem);
            }
        }
    }

    private static NSMenuItem? CreateNativeMenuItem(NativeMenuItemBase item)
    {
        if (item is NativeMenuItemSeparator)
        {
            return NSMenuItem.SeparatorItem;
        }

        if (item is not NativeMenuItem menuItem || !menuItem.IsVisible)
        {
            return null;
        }

        var nativeItem = new NSMenuItem
        {
            Title = menuItem.Header ?? string.Empty,
            Enabled = menuItem.IsEnabled
        };

        if (menuItem.Menu is { } submenu)
        {
            nativeItem.Submenu = CreateNativeMenu(submenu);
        }
        else
        {
            nativeItem.Activated += (_, _) => ((INativeMenuItemExporterEventsImplBridge)menuItem).RaiseClicked();
        }

        return nativeItem;
    }
}