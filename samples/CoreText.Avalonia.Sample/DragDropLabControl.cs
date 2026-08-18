using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace CoreText.Avalonia.Sample;

internal sealed class DragDropLabControl : Border, IDisposable
{
    private readonly string _artifactRoot;
    private readonly string _dragTextPath;
    private readonly TextBlock _status;
    private readonly TextBlock _details;

    public DragDropLabControl()
    {
        _artifactRoot = Path.Combine(Path.GetTempPath(), "CoreText.Avalonia.Sample", "dragdrop-lab");
        Directory.CreateDirectory(_artifactRoot);
        _dragTextPath = Path.Combine(_artifactRoot, "coretext-drag-note.txt");
        File.WriteAllText(_dragTextPath, CreateEditorNote(), System.Text.Encoding.UTF8);

        _status = new TextBlock
        {
            Text = "Drop a file from Finder or text from another app to inspect the payload.",
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#17324D")),
            TextWrapping = TextWrapping.Wrap
        };

        _details = new TextBlock
        {
            Text = "No external drop received yet.",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#5B7083")),
            TextWrapping = TextWrapping.Wrap
        };

        Child = BuildLayout();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dragTextPath))
            {
                File.Delete(_dragTextPath);
            }

            if (Directory.Exists(_artifactRoot) && !Directory.EnumerateFileSystemEntries(_artifactRoot).Any())
            {
                Directory.Delete(_artifactRoot);
            }
        }
        catch
        {
        }
    }

    private Control BuildLayout()
    {
        var layout = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 18
        };
        layout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        layout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        var sourceStack = new StackPanel
        {
            Spacing = 12
        };
        sourceStack.Children.Add(CreateSourceTile(
            "Drag text to TextEdit",
            "Press and drag this note into TextEdit, Notes, or another text-aware app.",
            CreateTextPayload));
        sourceStack.Children.Add(CreateSourceTile(
            "Drag file to Finder",
            "Press and drag the stable sample file into Finder, the desktop, or another file-aware app.",
            CreateFilePayload));
        sourceStack.Children.Add(CreateActionRow());

        var dropSurface = CreateDropSurface();

        layout.Children.Add(sourceStack);
        Grid.SetColumn(dropSurface, 1);
        layout.Children.Add(dropSurface);

        return layout;
    }

    private Control CreateActionRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        var revealButton = new Button
        {
            Content = "Reveal sample file",
            MinWidth = 150
        };
        revealButton.Click += (_, _) => NSWorkspace.SharedWorkspace.SelectFile(_dragTextPath, _artifactRoot);
        row.Children.Add(revealButton);

        var openEditorButton = new Button
        {
            Content = "Open in default editor",
            MinWidth = 170
        };
        openEditorButton.Click += (_, _) => NSWorkspace.SharedWorkspace.OpenUrl(new NSUrl(_dragTextPath, false));
        row.Children.Add(openEditorButton);

        return row;
    }

    private Border CreateSourceTile(string title, string description, Func<DataTransfer> payloadFactory)
    {
        var tile = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D9E4EE")),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 16,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#17324D"))
                    },
                    new TextBlock
                    {
                        Text = description,
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.Parse("#5B7083")),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Press and drag",
                        FontSize = 12,
                        FontWeight = FontWeight.Medium,
                        Foreground = new SolidColorBrush(Color.Parse("#0E7490"))
                    }
                }
            }
        };

        tile.AddHandler(InputElement.PointerPressedEvent, async (_, e) =>
        {
            if (!e.GetCurrentPoint(tile).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var effect = await DragDrop.DoDragDropAsync(e, payloadFactory(), DragDropEffects.Copy | DragDropEffects.Move);
            _status.Text = effect == DragDropEffects.None
                ? "Drag cancelled or rejected by the destination app."
                : $"Drag completed with effect: {effect}.";
            _details.Text = title;
            e.Handled = true;
        });

        return tile;
    }

    private Border CreateDropSurface()
    {
        var host = new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.Parse("#F7FBFD")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D9E4EE")),
            BorderThickness = new Thickness(1),
            MinHeight = 254,
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Drop from Finder or another app",
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#17324D"))
                    },
                    new TextBlock
                    {
                        Text = "Finder file drops and external text drops should both land here. The summary below shows which payload path won.",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.Parse("#5B7083")),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Border
                    {
                        Padding = new Thickness(18),
                        CornerRadius = new CornerRadius(14),
                        Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#C9D9E6")),
                        BorderThickness = new Thickness(1),
                        Child = new StackPanel
                        {
                            Spacing = 8,
                            Children =
                            {
                                _status,
                                _details
                            }
                        }
                    }
                }
            }
        };

        DragDrop.SetAllowDrop(host, true);
        host.AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        host.AddHandler(DragDrop.DropEvent, HandleDrop);
        host.AddHandler(DragDrop.DragLeaveEvent, (_, _) =>
        {
            host.BorderBrush = new SolidColorBrush(Color.Parse("#D9E4EE"));
        });

        return host;
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        var text = e.DataTransfer.TryGetText();
        e.DragEffects = files is { Length: > 0 } || !string.IsNullOrWhiteSpace(text)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        if (sender is Border host)
        {
            host.BorderBrush = e.DragEffects == DragDropEffects.None
                ? new SolidColorBrush(Color.Parse("#D9E4EE"))
                : new SolidColorBrush(Color.Parse("#0E7490"));
        }

        e.Handled = true;
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        if (sender is Border host)
        {
            host.BorderBrush = new SolidColorBrush(Color.Parse("#D9E4EE"));
        }

        var files = e.DataTransfer.TryGetFiles();
        var text = e.DataTransfer.TryGetText();
        if (files is { Length: > 0 })
        {
            _status.Text = $"Received {files.Length} file item(s) from Finder or another file-aware app.";
            _details.Text = string.Join(Environment.NewLine, files.Select(static file => file.Path.IsAbsoluteUri ? file.Path.LocalPath : file.Name));
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var normalized = text.ReplaceLineEndings(" ").Trim();
            if (normalized.Length > 220)
            {
                normalized = normalized[..217] + "...";
            }

            _status.Text = "Received text payload from another app.";
            _details.Text = normalized;
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        _status.Text = "Drop arrived, but no supported file or text payload was exposed.";
        _details.Text = string.Join(", ", e.DataTransfer.Formats.Select(static format => format.ToString()));
        e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private DataTransfer CreateTextPayload()
    {
        var transfer = new DataTransfer();
        var item = new DataTransferItem();
        item.SetText("CoreText.Avalonia drag session\nThis text came from the macOS sample drag lab.");
        item.Set(DataFormat.CreateStringPlatformFormat("public.html"), "<b>CoreText.Avalonia</b> drag session from the sample lab.");
        item.Set(DataFormat.CreateStringPlatformFormat("public.json"), "{\"source\":\"sample\",\"kind\":\"text\"}");
        transfer.Add(item);
        return transfer;
    }

    private DataTransfer CreateFilePayload()
    {
        var transfer = new DataTransfer();
        var item = new DataTransferItem();
        if (TopLevel.GetTopLevel(this)?.StorageProvider.TryGetFileFromPathAsync(new Uri(_dragTextPath, UriKind.Absolute)).GetAwaiter().GetResult() is { } file)
        {
            item.SetFile(file);
        }

        item.SetText(File.ReadAllText(_dragTextPath));
        transfer.Add(item);
        return transfer;
    }

    private static string CreateEditorNote()
    {
        return string.Join(Environment.NewLine,
        [
            "CoreText.Avalonia drag-drop exercise",
            string.Empty,
            "This file is produced by the sample's drag lab.",
            "Drag it into Finder to validate file-url export, or drop external text/files back into the sample target."
        ]);
    }
}