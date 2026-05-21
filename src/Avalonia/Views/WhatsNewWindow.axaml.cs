using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace TEKLauncher.Avalonia.Views;

public partial class WhatsNewWindow : Window
{
    static readonly IBrush s_headerBackground = new SolidColorBrush(Color.Parse("#8021252B"));
    static readonly IBrush s_headerForeground = new SolidColorBrush(Color.Parse("#C5D4E3"));
    static readonly IBrush s_itemForeground = new SolidColorBrush(Color.Parse("#C0D0E0"));

    public WhatsNewWindow()
    {
        InitializeComponent();
        Title = Locale.Get("whatsNew");
        LoadEntries();
    }

    void LoadEntries()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://TEKLauncher/res/Changelog.txt"));
            using var reader = new StreamReader(stream);
            for (string? version; (version = reader.ReadLine()) is not null;)
            {
                Root.Children.Add(new Border
                {
                    Margin = new Thickness(0, 8, 0, 8),
                    Padding = new Thickness(8, 4),
                    Background = s_headerBackground,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Child = new TextBlock
                    {
                        Foreground = s_headerForeground,
                        FontSize = 24,
                        Text = version,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                });

                if (!int.TryParse(reader.ReadLine(), out int groupCount))
                    break;

                for (int i = 0; i < groupCount; i++)
                {
                    string title = reader.ReadLine() ?? string.Empty;
                    Root.Children.Add(new TextBlock
                    {
                        Margin = new Thickness(20, 0, 0, 0),
                        Foreground = s_itemForeground,
                        Text = title
                    });

                    var builder = new StringBuilder();
                    if (!int.TryParse(reader.ReadLine(), out int lineCount))
                        lineCount = 0;

                    for (int j = 0; j < lineCount; j++)
                    {
                        string? line = reader.ReadLine();
                        if (line is not null)
                            builder.AppendLine(line);
                    }

                    Root.Children.Add(new TextBlock
                    {
                        Margin = new Thickness(50, 0, 0, 0),
                        FontSize = 18,
                        TextWrapping = TextWrapping.Wrap,
                        Text = builder.ToString().TrimEnd()
                    });
                }
            }

            if (Root.Children.Count > 0)
                return;
        }
        catch
        {
        }

        Root.Children.Add(new TextBlock
        {
            Text = "No changelog is available for this build.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = s_itemForeground
        });
    }
}