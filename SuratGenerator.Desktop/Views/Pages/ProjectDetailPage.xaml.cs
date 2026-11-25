using System;
using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class ProjectDetailPage : Page
{
    private readonly ProjectDetailViewModel _viewModel;
    private readonly Guid _projectId;

    public ProjectDetailPage()
    {
        InitializeComponent();
        _projectId = App.Navigation.TakeParameter() is Guid id ? id : Guid.Empty;
        _viewModel = new ProjectDetailViewModel(
            App.Projects,
            App.Navigation,
            App.FilePicker,
            App.ProjectDialogs,
            App.Explorer,
            new ConfirmationService(),
            App.DocumentFieldValues);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        if (_projectId != Guid.Empty)
        {
            await _viewModel.InitializeAsync(_projectId);
        }
    }
}
