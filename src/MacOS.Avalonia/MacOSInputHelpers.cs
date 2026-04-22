namespace MacOS.Avalonia;

internal static class MacOSInputHelpers
{
    public static RawInputModifiers ToRawInputModifiers(NSEventModifierMask modifierFlags)
    {
        var modifiers = RawInputModifiers.None;

        if (modifierFlags.HasFlag(NSEventModifierMask.ShiftKeyMask))
        {
            modifiers |= RawInputModifiers.Shift;
        }

        if (modifierFlags.HasFlag(NSEventModifierMask.ControlKeyMask))
        {
            modifiers |= RawInputModifiers.Control;
        }

        if (modifierFlags.HasFlag(NSEventModifierMask.AlternateKeyMask))
        {
            modifiers |= RawInputModifiers.Alt;
        }

        if (modifierFlags.HasFlag(NSEventModifierMask.CommandKeyMask))
        {
            modifiers |= RawInputModifiers.Meta;
        }

        return modifiers;
    }

    public static PhysicalKey ToPhysicalKey(ushort keyCode)
    {
        return keyCode switch
        {
            0 => PhysicalKey.A,
            1 => PhysicalKey.S,
            2 => PhysicalKey.D,
            3 => PhysicalKey.F,
            4 => PhysicalKey.H,
            5 => PhysicalKey.G,
            6 => PhysicalKey.Z,
            7 => PhysicalKey.X,
            8 => PhysicalKey.C,
            9 => PhysicalKey.V,
            11 => PhysicalKey.B,
            12 => PhysicalKey.Q,
            13 => PhysicalKey.W,
            14 => PhysicalKey.E,
            15 => PhysicalKey.R,
            16 => PhysicalKey.Y,
            17 => PhysicalKey.T,
            18 => PhysicalKey.Digit1,
            19 => PhysicalKey.Digit2,
            20 => PhysicalKey.Digit3,
            21 => PhysicalKey.Digit4,
            22 => PhysicalKey.Digit6,
            23 => PhysicalKey.Digit5,
            24 => PhysicalKey.Equal,
            25 => PhysicalKey.Digit9,
            26 => PhysicalKey.Digit7,
            27 => PhysicalKey.Minus,
            28 => PhysicalKey.Digit8,
            29 => PhysicalKey.Digit0,
            30 => PhysicalKey.BracketRight,
            31 => PhysicalKey.O,
            32 => PhysicalKey.U,
            33 => PhysicalKey.BracketLeft,
            34 => PhysicalKey.I,
            35 => PhysicalKey.P,
            36 => PhysicalKey.Enter,
            37 => PhysicalKey.L,
            38 => PhysicalKey.J,
            39 => PhysicalKey.Quote,
            40 => PhysicalKey.K,
            41 => PhysicalKey.Semicolon,
            42 => PhysicalKey.Backslash,
            43 => PhysicalKey.Comma,
            44 => PhysicalKey.Slash,
            45 => PhysicalKey.N,
            46 => PhysicalKey.M,
            47 => PhysicalKey.Period,
            48 => PhysicalKey.Tab,
            49 => PhysicalKey.Space,
            50 => PhysicalKey.Backquote,
            51 => PhysicalKey.Backspace,
            53 => PhysicalKey.Escape,
            54 => PhysicalKey.MetaRight,
            55 => PhysicalKey.MetaLeft,
            56 => PhysicalKey.ShiftLeft,
            57 => PhysicalKey.CapsLock,
            58 => PhysicalKey.AltLeft,
            59 => PhysicalKey.ControlLeft,
            60 => PhysicalKey.ShiftRight,
            61 => PhysicalKey.AltRight,
            62 => PhysicalKey.ControlRight,
            65 => PhysicalKey.NumPadDecimal,
            67 => PhysicalKey.NumPadMultiply,
            69 => PhysicalKey.NumPadAdd,
            71 => PhysicalKey.NumLock,
            75 => PhysicalKey.NumPadDivide,
            76 => PhysicalKey.NumPadEnter,
            78 => PhysicalKey.NumPadSubtract,
            81 => PhysicalKey.NumPadEqual,
            82 => PhysicalKey.NumPad0,
            83 => PhysicalKey.NumPad1,
            84 => PhysicalKey.NumPad2,
            85 => PhysicalKey.NumPad3,
            86 => PhysicalKey.NumPad4,
            87 => PhysicalKey.NumPad5,
            88 => PhysicalKey.NumPad6,
            89 => PhysicalKey.NumPad7,
            91 => PhysicalKey.NumPad8,
            92 => PhysicalKey.NumPad9,
            96 => PhysicalKey.F5,
            97 => PhysicalKey.F6,
            98 => PhysicalKey.F7,
            99 => PhysicalKey.F3,
            100 => PhysicalKey.F8,
            101 => PhysicalKey.F9,
            103 => PhysicalKey.F11,
            105 => PhysicalKey.F13,
            106 => PhysicalKey.F16,
            107 => PhysicalKey.F14,
            109 => PhysicalKey.F10,
            111 => PhysicalKey.F12,
            113 => PhysicalKey.F15,
            114 => PhysicalKey.Help,
            115 => PhysicalKey.Home,
            116 => PhysicalKey.PageUp,
            117 => PhysicalKey.Delete,
            118 => PhysicalKey.F4,
            119 => PhysicalKey.End,
            120 => PhysicalKey.F2,
            121 => PhysicalKey.PageDown,
            122 => PhysicalKey.F1,
            123 => PhysicalKey.ArrowLeft,
            124 => PhysicalKey.ArrowRight,
            125 => PhysicalKey.ArrowDown,
            126 => PhysicalKey.ArrowUp,
            _ => PhysicalKey.None
        };
    }

    public static Key ToKey(ushort keyCode, string? charactersIgnoringModifiers)
    {
        var physicalKey = ToPhysicalKey(keyCode);
        if (physicalKey != PhysicalKey.None)
        {
            return physicalKey.ToQwertyKey();
        }

        return charactersIgnoringModifiers switch
        {
            "\r" => Key.Enter,
            "\t" => Key.Tab,
            " " => Key.Space,
            _ => Key.None
        };
    }

    public static string? GetKeySymbol(NSEvent keyEvent, PhysicalKey physicalKey, RawInputModifiers modifiers)
    {
        if (!string.IsNullOrEmpty(keyEvent.CharactersIgnoringModifiers))
        {
            return keyEvent.CharactersIgnoringModifiers;
        }

        return physicalKey.ToQwertyKeySymbol(modifiers.HasFlag(RawInputModifiers.Shift));
    }

    public static string? GetText(NSEvent keyEvent)
    {
        var text = keyEvent.Characters;
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        foreach (var ch in text)
        {
            if (!char.IsControl(ch) || ch == '\t' || ch == '\r')
            {
                return text;
            }
        }

        return null;
    }
}