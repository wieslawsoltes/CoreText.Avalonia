using System;
using System.Runtime.InteropServices;

namespace MacOS.Avalonia;

internal static class MacOSDispatcherInterop
{
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreFoundationLibrary)]
    public static extern IntPtr CFRunLoopGetMain();

    [DllImport(CoreFoundationLibrary)]
    public static extern void CFRunLoopWakeUp(IntPtr runLoop);
}
