using System.IO;

namespace MacOS.Avalonia;

internal sealed class MacOSIconLoader : IPlatformIconLoader
{
    private sealed class IconStub(IBitmapImpl bitmap) : IWindowIconImpl
    {
        private readonly IBitmapImpl _bitmap = bitmap;

        public void Save(Stream outputStream)
        {
            _bitmap.Save(outputStream);
        }
    }

    public IWindowIconImpl LoadIcon(string fileName)
    {
        return new IconStub(AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>().LoadBitmap(fileName));
    }

    public IWindowIconImpl LoadIcon(Stream stream)
    {
        return new IconStub(AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>().LoadBitmap(stream));
    }

    public IWindowIconImpl LoadIcon(IBitmapImpl bitmap)
    {
        return new IconStub(bitmap);
    }
}
