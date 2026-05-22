using Avalonia.Data;
using System.ComponentModel;
using Avalonia.Markup.Xaml;
using TEKLauncher.Data;

namespace TEKLauncher.UI;

public class TrExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = LocaleBindingSource.Instance };
}

sealed class LocaleBindingSource : INotifyPropertyChanged
{
    public static LocaleBindingSource Instance { get; } = new();

    LocaleBindingSource() => Locale.LanguageChanged += OnLanguageChanged;

    public string this[string key] => Locale.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnLanguageChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
}
