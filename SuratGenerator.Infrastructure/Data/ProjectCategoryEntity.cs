namespace SuratGenerator.Infrastructure.Data;

public sealed class ProjectCategoryEntity
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? SourceCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public string FolderPath { get; set; } = string.Empty;
}
