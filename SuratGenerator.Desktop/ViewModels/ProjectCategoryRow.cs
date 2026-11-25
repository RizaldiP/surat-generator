using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class ProjectCategoryRow
{
    public ProjectCategoryRow(ProjectCategoryInfo category)
    {
        Category = category;
    }

    public ProjectCategoryInfo Category { get; }

    public Guid Id => Category.Id;

    public string Name => Category.Name;

    public int SortOrder => Category.SortOrder;

    public string FolderPath => Category.FolderPath;
}
