using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TEKLauncher.Avalonia.ViewModels;

public abstract class LauncherSectionScreenViewModel : INotifyPropertyChanged
{
    protected LauncherSectionScreenViewModel(LauncherSection section)
    {
        Section = section;
    }

    public LauncherSection Section { get; }

    public string Title => Locale.Get(LauncherShellNavigation.GetInfo(Section).TitleCode);

    public string Description => LauncherShellNavigation.GetInfo(Section).Description;

    public virtual void Activate()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }
}