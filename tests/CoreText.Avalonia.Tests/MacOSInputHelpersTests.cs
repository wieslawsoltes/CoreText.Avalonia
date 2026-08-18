using AppKit;
using Avalonia.Input;

namespace CoreText.Avalonia.Tests;

public sealed class MacOSInputHelpersTests
{
    [Fact]
    public void ModifierMaskMapsToAvaloniaModifiers()
    {
        var modifiers = MacOS.Avalonia.MacOSInputHelpers.ToRawInputModifiers(
            NSEventModifierMask.ShiftKeyMask
            | NSEventModifierMask.ControlKeyMask
            | NSEventModifierMask.AlternateKeyMask
            | NSEventModifierMask.CommandKeyMask);

        Assert.Equal(
            RawInputModifiers.Shift
            | RawInputModifiers.Control
            | RawInputModifiers.Alt
            | RawInputModifiers.Meta,
            modifiers);
    }

    [Theory]
    [InlineData((ushort)0, PhysicalKey.A)]
    [InlineData((ushort)36, PhysicalKey.Enter)]
    [InlineData((ushort)49, PhysicalKey.Space)]
    [InlineData((ushort)55, PhysicalKey.MetaLeft)]
    [InlineData((ushort)123, PhysicalKey.ArrowLeft)]
    [InlineData((ushort)126, PhysicalKey.ArrowUp)]
    public void PhysicalKeyMapCoversCommonMacVirtualKeys(ushort keyCode, PhysicalKey expected)
    {
        var physicalKey = MacOS.Avalonia.MacOSInputHelpers.ToPhysicalKey(keyCode);

        Assert.Equal(expected, physicalKey);
    }

    [Theory]
    [InlineData((ushort)0, "a", Key.A)]
    [InlineData((ushort)36, "\r", Key.Enter)]
    [InlineData((ushort)49, " ", Key.Space)]
    [InlineData((ushort)123, null, Key.Left)]
    public void KeyMapUsesPhysicalKeyTranslation(ushort keyCode, string? charactersIgnoringModifiers, Key expected)
    {
        var key = MacOS.Avalonia.MacOSInputHelpers.ToKey(keyCode, charactersIgnoringModifiers);

        Assert.Equal(expected, key);
    }
}