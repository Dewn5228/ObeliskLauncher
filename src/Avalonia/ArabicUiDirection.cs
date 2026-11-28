using Avalonia.Controls;
using Avalonia.Media;
using ObeliskLauncher.Data;

namespace ObeliskLauncher.Avalonia;

/// <summary>Mirrors layout only when the launcher uses Arabic.</summary>
internal static class ArabicUiDirection
{
    internal static void Apply(Window window)
    {
        window.FlowDirection = Locale.CurrentLanguage == "ar"
            ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
}
