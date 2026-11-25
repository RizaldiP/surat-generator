using System.Windows.Controls;

namespace SuratGenerator.Desktop.Services;

internal interface INavigationService
{
    void NavigateTo<TPage>(object? parameter = null) where TPage : Page;

    object? TakeParameter();
}
