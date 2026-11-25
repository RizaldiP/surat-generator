using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SuratGenerator.Desktop.Services;

internal sealed class AppThemeService : IDisposable
{
    private const string PalettePrefix = "pack://application:,,,/Resources/Themes/";
    private Window? _window;

    public void Initialize(Window window)
    {
        _window = window;
        SystemThemeWatcher.Watch(window, WindowBackdropType.Acrylic, true);
        ApplicationThemeManager.Changed += OnThemeChanged;
        ApplyPalette(ApplicationThemeManager.GetAppTheme());
    }

    public void Dispose()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        if (_window is not null)
        {
            try
            {
                SystemThemeWatcher.UnWatch(_window);
            }
            catch (InvalidOperationException)
            {
            }

            _window = null;
        }
    }

    private static void OnThemeChanged(ApplicationTheme theme, Color _)
    {
        ApplyPalette(theme);
    }

    private static void ApplyPalette(ApplicationTheme theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var dictionarySource = dictionaries[i].Source?.OriginalString;
            if (dictionarySource is not null && dictionarySource.StartsWith(PalettePrefix, StringComparison.OrdinalIgnoreCase))
            {
                dictionaries.RemoveAt(i);
            }
        }

        var source = theme == ApplicationTheme.Dark
            ? new Uri("pack://application:,,,/Resources/Themes/Dark.xaml", UriKind.Absolute)
            : new Uri("pack://application:,,,/Resources/Themes/Light.xaml", UriKind.Absolute);

        dictionaries.Add(new ResourceDictionary { Source = source });
    }
}
