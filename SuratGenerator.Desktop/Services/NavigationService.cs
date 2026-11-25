using System.Windows.Controls;

namespace SuratGenerator.Desktop.Services;

internal sealed class NavigationService : INavigationService
{
    private Action<Type>? _navigate;
    private object? _parameter;

    public void Register(Action<Type> navigate)
    {
        _navigate = navigate;
    }

    public void Unregister()
    {
        _navigate = null;
        _parameter = null;
    }

    public void NavigateTo<TPage>(object? parameter = null) where TPage : Page
    {
        if (_navigate is null)
        {
            return;
        }

        _parameter = parameter;
        _navigate(typeof(TPage));
    }

    public object? TakeParameter()
    {
        var parameter = _parameter;
        _parameter = null;
        return parameter;
    }
}
