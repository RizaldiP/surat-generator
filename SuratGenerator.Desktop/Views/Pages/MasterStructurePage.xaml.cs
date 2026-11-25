using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SuratGenerator.Core.Models;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class MasterStructurePage : Page
{
    private readonly MasterStructureViewModel _viewModel;
    private Guid? _draggedCategoryId;

    public MasterStructurePage()
    {
        InitializeComponent();
        _viewModel = new MasterStructureViewModel(
            App.DocumentCategories,
            App.CategoryChecklistItems,
            new MasterStructureDialogService(),
            new ConfirmationService());
        DataContext = _viewModel;

        Loaded += OnLoaded;
        CategoryListView.SelectionChanged += OnCategorySelectionChanged;
        CategoryListView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        CategoryListView.DragOver += OnDragOver;
        CategoryListView.Drop += OnDrop;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.LoadAsync();
    }

    private async void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var category = e.AddedItems.Count > 0 ? e.AddedItems[0] as DocumentCategoryInfo : null;
        if (category is null && e.RemovedItems.Count > 0)
        {
            await _viewModel.SelectCategoryAsync(null);
            return;
        }

        if (category is not null)
        {
            await _viewModel.SelectCategoryAsync(category);
        }
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (source is null || FindAncestor<ButtonBase>(source) is not null)
        {
            return;
        }

        if (FindAncestor<ListViewItem>(source)?.DataContext is not DocumentCategoryInfo category)
        {
            return;
        }

        _draggedCategoryId = category.Id;
        DragDrop.DoDragDrop(CategoryListView, category.Id, DragDropEffects.Move);
        _draggedCategoryId = null;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = _draggedCategoryId.HasValue ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (_draggedCategoryId is not Guid draggedId)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        if (source is not null
            && FindAncestor<ListViewItem>(source)?.DataContext is DocumentCategoryInfo target)
        {
            await _viewModel.MoveCategoryAsync(draggedId, target.Id);
        }

        _draggedCategoryId = null;
    }

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
