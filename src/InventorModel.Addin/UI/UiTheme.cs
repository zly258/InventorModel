using System;
using System.Windows;

namespace InventorModel.Addin;

internal static class UiTheme
{
    private static readonly Uri ThemeUri = new Uri(
        "pack://application:,,,/InventorModel.Addin;component/UI/UnifiedTheme.xaml",
        UriKind.Absolute);

    public static void Apply(Window window)
    {
        if (window == null) return;
        window.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = ThemeUri });
    }

}
