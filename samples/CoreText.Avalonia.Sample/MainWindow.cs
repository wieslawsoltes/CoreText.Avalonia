using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoreText.Avalonia.Sample;

internal sealed class MainWindow : Window
{
    public MainWindow(CoreTextSurfaceMode surfaceMode)
    {
        Title = $"CoreText.Avalonia Render Gallery ({surfaceMode})";
        Width = 1480;
        Height = 920;
        MinWidth = 1220;
        MinHeight = 780;
        Background = new SolidColorBrush(Color.Parse("#EAF2F8"));

        var sampleBitmap = SampleBitmapFactory.Create();
        var renderGallery = new RenderGalleryControl(sampleBitmap)
        {
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var controlPanel = CreateControlPanel();
        Grid.SetColumn(controlPanel, 1);

        Content = new Border
        {
            Margin = new Thickness(28),
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(22),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D6E1EB")),
            BorderThickness = new Thickness(1),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1.55, GridUnitType.Star)),
                    new ColumnDefinition(420, GridUnitType.Pixel)
                },
                ColumnSpacing = 24,
                Children =
                {
                    renderGallery,
                    controlPanel
                }
            }
        };
    }

    private static Control CreateControlPanel()
    {
        var accent = new SolidColorBrush(Color.Parse("#0E7490"));
        var heading = new SolidColorBrush(Color.Parse("#17324D"));
        var muted = new SolidColorBrush(Color.Parse("#5B7083"));

        var stack = new StackPanel
        {
            Spacing = 16
        };

        stack.Children.Add(CreateCard(
            CreateTextBlock("Controls", 24, heading, FontWeight.SemiBold),
            CreateTextBlock("The left canvas now uses FormattedText via the normal Avalonia formatter.", 13, muted),
            CreateTextBlock("The controls below use standard control text and rich TextBlock inlines.", 13, muted),
            CreateRichTextPreview(muted, heading, accent)));

        stack.Children.Add(CreateCard(
            CreateTextBlock("Buttons and toggles", 17, heading, FontWeight.SemiBold),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    new Button
                    {
                        MinWidth = 136,
                        Content = "Measure text"
                    },
                    new Button
                    {
                        MinWidth = 136,
                        Content = "Snapshot"
                    }
                }
            },
            new CheckBox
            {
                IsChecked = true,
                Content = "Platform fallback"
            },
            new RadioButton
            {
                GroupName = "surface-mode",
                IsChecked = true,
                Content = "Auto surface"
            },
            new RadioButton
            {
                GroupName = "surface-mode",
                Content = "Software surface"
            },
            new ToggleButton
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Content = "Underline accent"
            }));

        stack.Children.Add(CreateCard(
            CreateTextBlock("Value controls", 17, heading, FontWeight.SemiBold),
            CreateTextBlock("These controls share the same surface while text is rendered through TextLayout.", 13, muted),
            new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = 64
            },
            new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 64,
                Height = 10
            },
            new ScrollBar
            {
                Orientation = Orientation.Horizontal,
                Minimum = 0,
                Maximum = 100,
                Value = 42,
                ViewportSize = 16
            },
            new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 42,
                Height = 10
            }));

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = stack
        };
    }

    private static TextBlock CreateTextBlock(
        string text,
        double fontSize,
        IBrush foreground,
        FontWeight weight = FontWeight.Normal,
        FontStyle style = FontStyle.Normal)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            FontStyle = style,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock CreateRichTextPreview(IBrush muted, IBrush heading, IBrush accent)
    {
        var textBlock = new TextBlock
        {
            FontSize = 15,
            Foreground = muted,
            TextWrapping = TextWrapping.Wrap
        };

        textBlock.Inlines!.Add(new Run("Rich text: "));
        textBlock.Inlines.Add(new Run("bold") { FontWeight = FontWeight.Bold, Foreground = heading });
        textBlock.Inlines.Add(new Run(" "));
        textBlock.Inlines.Add(new Run("italic") { FontStyle = FontStyle.Italic, Foreground = heading });
        textBlock.Inlines.Add(new Run(" "));
        textBlock.Inlines.Add(new Run("underline") { TextDecorations = TextDecorations.Underline, Foreground = accent, FontWeight = FontWeight.Medium });
        textBlock.Inlines.Add(new Run(" "));
        textBlock.Inlines.Add(new Run("accent") { FontStyle = FontStyle.Italic, FontWeight = FontWeight.SemiBold, TextDecorations = TextDecorations.Underline, Foreground = accent });

        return textBlock;
    }

    private static Border CreateCard(params Control[] children)
    {
        var stack = new StackPanel
        {
            Spacing = 12
        };

        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.Parse("#F7FAFD")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D9E4EE")),
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }
}
