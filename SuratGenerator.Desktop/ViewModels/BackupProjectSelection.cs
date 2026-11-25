using CommunityToolkit.Mvvm.ComponentModel;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class BackupProjectSelection : ObservableObject
{
    private bool _isSelected;

    public BackupProjectSelection(BackupProjectInfo project)
    {
        Project = project;
    }

    public BackupProjectInfo Project { get; }

    public string DisplayName => Project.Name;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
