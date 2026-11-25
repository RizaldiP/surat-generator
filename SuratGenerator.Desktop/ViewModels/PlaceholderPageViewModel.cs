using CommunityToolkit.Mvvm.ComponentModel;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class PlaceholderPageViewModel : ObservableObject
{
    public PlaceholderPageViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public string Title { get; }

    public string Message { get; }
}
