using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoreText.Avalonia.Sample;

internal sealed class MainWindow : Window
{
    public MainWindow(CoreTextSurfaceMode surfaceMode)
    {
        Title = $"CoreText.Avalonia Render Gallery ({surfaceMode})";
        Width = 1460;
        Height = 940;
        MinWidth = 1180;
        MinHeight = 760;
        Background = new SolidColorBrush(Color.Parse("#EAF2F8"));

        var sampleBitmap = SampleBitmapFactory.Create();
        Closed += (_, _) => sampleBitmap.Dispose();

        var shell = new Grid
        {
            RowSpacing = 20
        };
        shell.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        shell.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));

        var header = CreateHeader(surfaceMode);
        shell.Children.Add(header);

        var tabs = CreateTabs(surfaceMode, sampleBitmap);
        Grid.SetRow(tabs, 1);
        shell.Children.Add(tabs);

        Content = new Border
        {
            Margin = new Thickness(26),
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(24),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D7E3ED")),
            BorderThickness = new Thickness(1),
            Child = shell
        };
    }

    private static Control CreateHeader(CoreTextSurfaceMode surfaceMode)
    {
        var grid = new Grid
        {
            ColumnSpacing = 18
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(310, GridUnitType.Pixel)));

        var intro = new StackPanel
        {
            Spacing = 10
        };
        intro.Children.Add(new TextBlock
        {
            Text = "CoreText.Avalonia Sample Gallery",
            FontSize = 30,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#17324D"))
        });
        intro.Children.Add(new TextBlock
        {
            Text = "The sample is organized into focused tabs so rendering, text, effects, and control coverage are easier to inspect independently.",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.Parse("#5B7083")),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 760
        });

        var badgeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        badgeRow.Children.Add(CreateBadge($"{surfaceMode} surface", "#0E7490", "#D8F0F4"));
        badgeRow.Children.Add(CreateBadge("Rendering", "#1D4ED8", "#DBEAFE"));
        badgeRow.Children.Add(CreateBadge("Text", "#7C3AED", "#EDE9FE"));
        badgeRow.Children.Add(CreateBadge("Controls", "#0F766E", "#DDF5F0"));
        intro.Children.Add(badgeRow);

        grid.Children.Add(intro);

        var summary = CreateCard(
            "Session layout",
            "Tabs group the sample by renderer capability instead of mixing every scenario into one surface.",
            CreateInfoRow("Rendering", "Brushes, geometry, masks, shadows, effects"),
            CreateInfoRow("Text & Layout", "FormattedText, hierarchy, rich inlines"),
            CreateInfoRow("Controls", "Inputs, selection, values, and action states"));
        Grid.SetColumn(summary, 1);
        grid.Children.Add(summary);

        return grid;
    }

    private static Control CreateTabs(CoreTextSurfaceMode surfaceMode, WriteableBitmap sampleBitmap)
    {
        var tabControl = new TabControl();
        tabControl.Items.Add(CreateTab("Rendering", CreateRenderingTab(sampleBitmap)));
        tabControl.Items.Add(CreateTab("Text & Layout", CreateTextTab(sampleBitmap)));
        tabControl.Items.Add(CreateTab("Controls", CreateControlsTab(surfaceMode)));
        return tabControl;
    }

    private static TabItem CreateTab(string header, Control content) =>
        new()
        {
            Header = header,
            Content = content
        };

    private static Control CreateRenderingTab(WriteableBitmap sampleBitmap)
    {
        var layout = CreateTwoColumnGrid();

        var brushesCard = CreateDemoCard(
            "Brushes and images",
            "Solid fills, gradients, tiled image brushes, and bitmap blits are grouped together in one surface.",
            new RenderGalleryControl(sampleBitmap, RenderGalleryScene.BrushesAndImages)
            {
                Height = 320
            });
        layout.Children.Add(brushesCard);

        var geometryCard = CreateDemoCard(
            "Geometry and masks",
            "Shapes, arc paths, combined geometry, and opacity masking are isolated into a separate scene.",
            new RenderGalleryControl(sampleBitmap, RenderGalleryScene.GeometryAndMasks)
            {
                Height = 320
            });
        Grid.SetColumn(geometryCard, 1);
        layout.Children.Add(geometryCard);

        var effectsCard = CreateDemoCard(
            "Effects and composition",
            "This scene exercises blur, layered opacity, and shadow-heavy composition without crowding the other drawing samples.",
            new RenderGalleryControl(sampleBitmap, RenderGalleryScene.EffectsAndComposition)
            {
                Height = 272
            });
        Grid.SetRow(effectsCard, 1);
        Grid.SetColumnSpan(effectsCard, 2);
        layout.Children.Add(effectsCard);

        return CreateTabHost(layout);
    }

    private static Control CreateTextTab(WriteableBitmap sampleBitmap)
    {
        var layout = CreateTwoColumnGrid();

        var formattedCanvasCard = CreateDemoCard(
            "FormattedText canvas",
            "The custom-drawn text surface focuses on hierarchy, sizing, alignment, and contrast through the normal Avalonia formatter.",
            new RenderGalleryControl(sampleBitmap, RenderGalleryScene.TextFormatting)
            {
                Height = 320
            });
        layout.Children.Add(formattedCanvasCard);

        var richTextCard = CreateCard(
            "Rich text inlines",
            "Inline styling is demonstrated with real TextBlock runs rather than a handwritten glyph preview.",
            CreateRichTextPreview(),
            CreateBodyText("This paragraph keeps normal wrapping and baseline flow while the highlighted runs exercise font weight, style, and decorations through TextLayout."),
            CreateQuoteBlock());
        Grid.SetColumn(richTextCard, 1);
        layout.Children.Add(richTextCard);

        var typographyCard = CreateCard(
            "Typography matrix",
            "The sample keeps text scenarios grouped: hierarchy on the left, dense body copy in the middle, and utility labels on the right.",
            CreateTypographyMatrix());
        Grid.SetRow(typographyCard, 1);
        Grid.SetColumnSpan(typographyCard, 2);
        layout.Children.Add(typographyCard);

        return CreateTabHost(layout);
    }

    private static Control CreateControlsTab(CoreTextSurfaceMode surfaceMode)
    {
        var layout = CreateTwoColumnGrid();

        var actionsCard = CreateCard(
            "Actions and toggles",
            "Primary actions stay aligned and the toggle states are grouped underneath instead of floating in a long vertical stack.",
            CreateButtonRow(),
            new ToggleButton
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 180,
                Content = "Underline accent"
            },
            new CheckBox
            {
                IsChecked = true,
                Content = "Platform fallback"
            });
        layout.Children.Add(actionsCard);

        var inputsCard = CreateCard(
            "Inputs and selection",
            "Text entry and source selection live together so label, watermark, and selected-item text can be checked in one place.",
            new TextBox
            {
                PlaceholderText = "Search renderer diagnostics",
                Text = "Resize regression fixed"
            },
            new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 104,
                Text = "The sample window now uses tabs and grouped cards.\nEach section is spaced consistently and easier to scan."
            },
            new ComboBox
            {
                SelectedIndex = 0,
                ItemsSource = new[]
                {
                    $"{surfaceMode} surface",
                    "Metal surface",
                    "Software surface"
                }
            });
        Grid.SetColumn(inputsCard, 1);
        layout.Children.Add(inputsCard);

        var interactionCard = CreateCard(
            "Selection, values, and status",
            "Selection controls sit beside the range/status controls so the shared text and chrome can be compared quickly.",
            CreateInteractionGrid());
        Grid.SetRow(interactionCard, 1);
        Grid.SetColumnSpan(interactionCard, 2);
        layout.Children.Add(interactionCard);

        return CreateTabHost(layout);
    }

    private static Control CreateTabHost(Control content) =>
        new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

    private static Grid CreateTwoColumnGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 18
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        return grid;
    }

    private static Border CreateDemoCard(string title, string description, Control content)
    {
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        return CreateCard(title, description, content);
    }

    private static Border CreateCard(string title, string description, params Control[] content)
    {
        var stack = new StackPanel
        {
            Spacing = 14
        };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#17324D"))
        });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#5B7083")),
            TextWrapping = TextWrapping.Wrap
        });

        foreach (var child in content)
        {
            stack.Children.Add(child);
        }

        return new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Color.Parse("#F7FAFD")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D9E4EE")),
            BorderThickness = new Thickness(1),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetY = 10,
                Blur = 18,
                Color = Color.FromArgb(34, 23, 50, 77)
            }),
            Child = stack
        };
    }

    private static Control CreateInfoRow(string label, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("96,*")
        };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#0E7490"))
        });

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#5B7083")),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
        return grid;
    }

    private static Control CreateButtonRow()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        var measure = new Button
        {
            MinWidth = 150,
            Content = "Measure text"
        };
        grid.Children.Add(measure);

        var snapshot = new Button
        {
            MinWidth = 150,
            Content = "Snapshot"
        };
        Grid.SetColumn(snapshot, 1);
        grid.Children.Add(snapshot);

        return grid;
    }

    private static Control CreateInteractionGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 18
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        var selectionStack = new StackPanel
        {
            Spacing = 12
        };
        selectionStack.Children.Add(new TextBlock
        {
            Text = "Selection controls",
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#17324D"))
        });
        selectionStack.Children.Add(new RadioButton
        {
            GroupName = "surface-mode",
            IsChecked = true,
            Content = "Auto surface"
        });
        selectionStack.Children.Add(new RadioButton
        {
            GroupName = "surface-mode",
            Content = "Software surface"
        });
        selectionStack.Children.Add(new CheckBox
        {
            IsChecked = true,
            Content = "Render rich text"
        });
        selectionStack.Children.Add(new ListBox
        {
            Height = 112,
            SelectedIndex = 1,
            ItemsSource = new[]
            {
                "Brushes and images",
                "Geometry and masks",
                "Effects and composition"
            }
        });
        grid.Children.Add(selectionStack);

        var valuesStack = new StackPanel
        {
            Spacing = 12
        };
        valuesStack.Children.Add(new TextBlock
        {
            Text = "Value controls",
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#17324D"))
        });
        valuesStack.Children.Add(new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 68
        });
        valuesStack.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 68,
            Height = 10
        });
        valuesStack.Children.Add(new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Minimum = 0,
            Maximum = 100,
            Value = 42,
            ViewportSize = 18
        });
        valuesStack.Children.Add(new TextBlock
        {
            Text = "Progress, slider, and scroll state share the same typography and chrome path.",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#5B7083")),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(valuesStack, 1);
        grid.Children.Add(valuesStack);

        return grid;
    }

    private static TextBlock CreateBodyText(string text) =>
        new()
        {
            Text = text,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.Parse("#5B7083")),
            TextWrapping = TextWrapping.Wrap
        };

    private static TextBlock CreateRichTextPreview()
    {
        var body = new SolidColorBrush(Color.Parse("#5B7083"));
        var heading = new SolidColorBrush(Color.Parse("#17324D"));
        var accent = new SolidColorBrush(Color.Parse("#0E7490"));

        var block = new TextBlock
        {
            FontSize = 16,
            Foreground = body,
            TextWrapping = TextWrapping.Wrap
        };

        block.Inlines!.Add(new Run("Rich text preview: "));
        block.Inlines.Add(new Run("bold") { FontWeight = FontWeight.Bold, Foreground = heading });
        block.Inlines.Add(new Run(" "));
        block.Inlines.Add(new Run("italic") { FontStyle = FontStyle.Italic, Foreground = heading });
        block.Inlines.Add(new Run(" "));
        block.Inlines.Add(new Run("underline") { TextDecorations = TextDecorations.Underline, Foreground = accent });
        block.Inlines.Add(new Run(" "));
        block.Inlines.Add(new Run("accent") { FontStyle = FontStyle.Italic, FontWeight = FontWeight.SemiBold, TextDecorations = TextDecorations.Underline, Foreground = accent });
        block.Inlines.Add(new Run(" "));
        block.Inlines.Add(new Run("with normal wrapping and spacing.") { Foreground = body });

        return block;
    }

    private static Border CreateQuoteBlock() =>
        new()
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.Parse("#E9F4F6")),
            Child = new TextBlock
            {
                Text = "“The sample should read like a lab, not a collage.”",
                FontSize = 17,
                FontStyle = FontStyle.Italic,
                FontWeight = FontWeight.Medium,
                Foreground = new SolidColorBrush(Color.Parse("#0E7490")),
                TextWrapping = TextWrapping.Wrap
            }
        };

    private static Control CreateTypographyMatrix()
    {
        var grid = new Grid
        {
            ColumnSpacing = 18
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        grid.Children.Add(CreateTypeColumn(
            "Hierarchy",
            CreateTypeLine("Display title", 28, FontWeight.SemiBold, "#17324D"),
            CreateTypeLine("Section heading", 20, FontWeight.SemiBold, "#17324D"),
            CreateTypeLine("Body copy for general explanation.", 15, FontWeight.Normal, "#5B7083"),
            CreateTypeLine("Accent detail", 14, FontWeight.Medium, "#0E7490")));

        var wrappedColumn = CreateTypeColumn(
            "Wrapping",
            CreateBodyText("Text wrapping is isolated from the custom canvas so layout issues can be spotted without geometry and image noise."),
            CreateBodyText("This group intentionally uses multiple line lengths and paragraph widths."),
            CreateBodyText("Rich inline content above should share the same text shaping path."));
        Grid.SetColumn(wrappedColumn, 1);
        grid.Children.Add(wrappedColumn);

        var utilityColumn = CreateTypeColumn(
            "Utility labels",
            CreateTypeLine("12 pt label", 12, FontWeight.Medium, "#0E7490"),
            CreateTypeLine("14 pt note", 14, FontWeight.Normal, "#5B7083"),
            CreateTypeLine("17 pt emphasis", 17, FontWeight.Medium, "#17324D", FontStyle.Italic),
            CreateTypeLine("22 pt callout", 22, FontWeight.SemiBold, "#17324D"));
        Grid.SetColumn(utilityColumn, 2);
        grid.Children.Add(utilityColumn);

        return grid;
    }

    private static Border CreateTypeColumn(string title, params Control[] children)
    {
        var stack = new StackPanel
        {
            Spacing = 10
        };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#0E7490"))
        });

        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(14),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D9E4EE")),
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    private static TextBlock CreateTypeLine(
        string text,
        double fontSize,
        FontWeight weight,
        string foregroundHex,
        FontStyle style = FontStyle.Normal) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            FontStyle = style,
            Foreground = new SolidColorBrush(Color.Parse(foregroundHex)),
            TextWrapping = TextWrapping.Wrap
        };

    private static Border CreateBadge(string text, string foregroundHex, string backgroundHex) =>
        new()
        {
            Padding = new Thickness(10, 5),
            CornerRadius = new CornerRadius(999),
            Background = new SolidColorBrush(Color.Parse(backgroundHex)),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeight.Medium,
                Foreground = new SolidColorBrush(Color.Parse(foregroundHex))
            }
        };
}
