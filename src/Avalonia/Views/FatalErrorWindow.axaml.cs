using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;

namespace TEKLauncher.Avalonia.Views;

public partial class FatalErrorWindow : Window
{
    public FatalErrorWindow()
    {
        InitializeComponent();
        Title = Locale.Get("errors.fatalError");
        FatalMessageText.Text = string.Concat(Locale.Get("errors.fatalErrorMessagePrefix"), " ", Locale.Get("errors.fatalErrorMessageLink"));
    }

    public FatalErrorWindow(Exception exception)
      : this()
    {
        ExceptionData.Text = exception.ToString();
    }

    void OpenDiscord(object? sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://discord.gg/JBUgcwvpfc") { UseShellExecute = true });

    async void CopyError(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || string.IsNullOrWhiteSpace(ExceptionData.Text))
            return;

        await clipboard.SetTextAsync(ExceptionData.Text);
        CopyErrorButton.Content = "Copied";
    }
}