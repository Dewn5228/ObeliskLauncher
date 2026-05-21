using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using TEKLauncher.Avalonia.Views;

namespace TEKLauncher.UI;

sealed class AvaloniaLauncherDialogService : ILauncherDialogService
{
    public void Show(string type, string message)
    {
        ShowDialog(new AvaloniaDialogWindow(Locale.Get(type), message, false));
    }

    public bool ShowOptions(string type, string message) => ShowDialog(new AvaloniaDialogWindow(Locale.Get(type), message, true)) ?? false;

    public void ShowDownloadErr(string name, string url)
    {
        string message = $"The launcher was unable to download {name}. Open the download page and place the file into the launcher data folder before restarting the launcher.";
        ShowDialog(new AvaloniaDialogWindow(Locale.Get("common.error"), message, false, "Open download page", url));
    }

    static bool? ShowDialog(AvaloniaDialogWindow dialog)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.Invoke(() => ShowDialog(dialog));

        Window? owner = GetOwner();
        bool? result = null;
        Exception? failure = null;
        var frame = new DispatcherFrame();
        Task<bool?> task = owner is not null && owner.IsVisible
          ? dialog.ShowDialog<bool?>(owner)
          : ShowStandalone(dialog);

        Complete(task, frame, value => result = value, ex => failure = ex);
        Dispatcher.UIThread.PushFrame(frame);

        if (failure is not null)
            throw failure;

        return result;
    }

    static async void Complete(Task<bool?> task, DispatcherFrame frame, Action<bool?> setResult, Action<Exception> setFailure)
    {
        try
        {
            setResult(await task);
        }
        catch (Exception ex)
        {
            setFailure(ex);
        }
        finally
        {
            frame.Continue = false;
        }
    }

    static Task<bool?> ShowStandalone(AvaloniaDialogWindow dialog)
    {
        var tcs = new TaskCompletionSource<bool?>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog.DialogResultValue);
        dialog.Show();
        return tcs.Task;
    }

    static Window? GetOwner() => (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}