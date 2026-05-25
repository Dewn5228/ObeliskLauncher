using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ObeliskLauncher.Avalonia.Views;

sealed class AvaloniaDialogWindow : Window
{
    public bool? DialogResultValue { get; private set; }

    public AvaloniaDialogWindow(string title, string message, bool twoOptions, string? linkLabel = null, string? linkUrl = null)
    {
        Title = title;
        Width = 560;
        Height = linkUrl is null ? 240 : 280;
        MinWidth = 420;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        if (linkUrl is not null)
        {
            var openLinkButton = new Button { Content = linkLabel ?? "Open link" };
            openLinkButton.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(linkUrl) { UseShellExecute = true });
            actions.Children.Add(openLinkButton);
        }

        if (twoOptions)
        {
            var noButton = new Button { Content = Locale.Get("common.no"), MinWidth = 90 };
            noButton.Click += (_, _) =>
            {
                DialogResultValue = false;
                Close(false);
            };
            actions.Children.Add(noButton);
        }

        var okLabel = twoOptions ? Locale.Get("common.yes") : Locale.Get("common.ok");
        var okButton = new Button { Content = okLabel, MinWidth = 90 };
        okButton.Click += (_, _) =>
        {
            DialogResultValue = true;
            Close(true);
        };
        actions.Children.Add(okButton);

        Content = new Border
        {
            Padding = new Thickness(20),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children =
        {
          new TextBlock
          {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
          },
          new Border
          {
            Margin = new Thickness(0, 20, 0, 0),
            Child = actions
          }
        }
            }
        };

        if (Content is Border { Child: Grid grid } && grid.Children.Count > 1)
            Grid.SetRow(grid.Children[1], 1);
    }
}