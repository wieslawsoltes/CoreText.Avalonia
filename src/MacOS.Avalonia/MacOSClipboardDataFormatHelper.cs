using Avalonia.Input;

namespace MacOS.Avalonia;

internal static class MacOSClipboardDataFormatHelper
{
    public const string Utf8PlainTextPasteboardType = "public.utf8-plain-text";
    public const string FileUrlPasteboardType = "public.file-url";
    public const string PngPasteboardType = "public.png";
    public const string UrlNamePasteboardType = "public.url-name";

    private const string HtmlPasteboardType = "public.html";
    private const string RtfPasteboardType = "public.rtf";
    private const string XmlPasteboardType = "public.xml";
    private const string JsonPasteboardType = "public.json";
    private const string AppPrefix = "net.avaloniaui.app.uti.";

    public static DataFormat ToDataFormat(string systemType)
    {
        return systemType switch
        {
            Utf8PlainTextPasteboardType => DataFormat.Text,
            FileUrlPasteboardType => DataFormat.File,
            PngPasteboardType => DataFormat.Bitmap,
            _ when IsTextType(systemType) => DataFormat.FromSystemName<string>(systemType, AppPrefix),
            _ => DataFormat.FromSystemName<byte[]>(systemType, AppPrefix)
        };
    }

    public static string ToSystemType(DataFormat format)
    {
        if (DataFormat.Text.Equals(format))
            return Utf8PlainTextPasteboardType;

        if (DataFormat.File.Equals(format))
            return FileUrlPasteboardType;

        if (DataFormat.Bitmap.Equals(format))
            return PngPasteboardType;

        return format.ToSystemName(AppPrefix);
    }

    private static bool IsTextType(string systemType)
    {
        if (string.IsNullOrWhiteSpace(systemType))
        {
            return false;
        }

        if (systemType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || systemType.StartsWith("public.utf", StringComparison.OrdinalIgnoreCase)
            || systemType.StartsWith("public.text", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (systemType.StartsWith(AppPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return HasTextLikeSuffix(systemType.AsSpan(AppPrefix.Length));
        }

        return systemType.Equals(HtmlPasteboardType, StringComparison.OrdinalIgnoreCase)
            || systemType.Equals(RtfPasteboardType, StringComparison.OrdinalIgnoreCase)
            || systemType.Equals(XmlPasteboardType, StringComparison.OrdinalIgnoreCase)
            || systemType.Equals(JsonPasteboardType, StringComparison.OrdinalIgnoreCase)
            || systemType.Equals(UrlNamePasteboardType, StringComparison.OrdinalIgnoreCase)
            || HasTextLikeSuffix(systemType);
    }

    private static bool HasTextLikeSuffix(ReadOnlySpan<char> identifier)
    {
        return identifier.EndsWith("html", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("htm", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("rtf", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("xml", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("json", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("yaml", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("csv", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("text", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("plain", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("source", StringComparison.OrdinalIgnoreCase)
            || identifier.EndsWith("script", StringComparison.OrdinalIgnoreCase);
    }
}