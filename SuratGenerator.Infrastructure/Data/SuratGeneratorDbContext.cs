using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Infrastructure.Data;

public sealed class SuratGeneratorDbContext : DbContext
{
    public SuratGeneratorDbContext(DbContextOptions<SuratGeneratorDbContext> options)
        : base(options)
    {
    }

    public DbSet<TemplateEntity> Templates => Set<TemplateEntity>();

    public DbSet<TemplateFieldEntity> TemplateFields => Set<TemplateFieldEntity>();

    public DbSet<DraftEntity> Drafts => Set<DraftEntity>();

    public DbSet<GeneratedDocumentEntity> GeneratedDocuments => Set<GeneratedDocumentEntity>();

    public DbSet<TemplateSyncStateEntity> TemplateSyncStates => Set<TemplateSyncStateEntity>();

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    public DbSet<ProjectFieldEntity> ProjectFields => Set<ProjectFieldEntity>();

    public DbSet<DocumentCategoryEntity> DocumentCategories => Set<DocumentCategoryEntity>();

    public DbSet<CategoryChecklistItemEntity> CategoryChecklistItems => Set<CategoryChecklistItemEntity>();

    public DbSet<MasterDocumentEntity> MasterDocuments => Set<MasterDocumentEntity>();

    public DbSet<MasterDocumentVersionEntity> MasterDocumentVersions => Set<MasterDocumentVersionEntity>();

    public DbSet<ProjectCategoryEntity> ProjectCategories => Set<ProjectCategoryEntity>();

    public DbSet<ProjectDocumentEntity> ProjectDocuments => Set<ProjectDocumentEntity>();

    public DbSet<DocumentFieldValueEntity> DocumentFieldValues => Set<DocumentFieldValueEntity>();

    public DbSet<PlaceholderMappingEntity> PlaceholderMappings => Set<PlaceholderMappingEntity>();

    public DbSet<BackupRecordEntity> BackupRecords => Set<BackupRecordEntity>();

    public DbSet<SchemaVersionEntity> SchemaVersions => Set<SchemaVersionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemplateEntity>(entity =>
        {
            entity.ToTable("Templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.SourceFileName).IsRequired();
            entity.Property(x => x.StoredFilePath).IsRequired();
            entity.Property(x => x.Category).IsRequired();
            entity.Property(x => x.Version).IsRequired();
            entity.HasMany(x => x.Fields)
                .WithOne(x => x.Template)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TemplateFieldEntity>(entity =>
        {
            entity.ToTable("TemplateFields");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired();
            entity.Property(x => x.Label).IsRequired();
            entity.Property(x => x.Kind).IsRequired();
            entity.HasIndex(x => new { x.TemplateId, x.Key }).IsUnique();
        });

        modelBuilder.Entity<DraftEntity>(entity =>
        {
            entity.ToTable("Drafts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TemplateName).IsRequired();
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.ValuesJson).IsRequired();
        });

        modelBuilder.Entity<GeneratedDocumentEntity>(entity =>
        {
            entity.ToTable("GeneratedDocuments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TemplateName).IsRequired();
            entity.Property(x => x.DocumentName).IsRequired();
            entity.Property(x => x.Kind).HasConversion<int>();
            entity.Property(x => x.FilePath).IsRequired();
        });

        modelBuilder.Entity<TemplateSyncStateEntity>(entity =>
        {
            entity.ToTable("DriveSyncStates");
            entity.HasKey(x => x.TemplateId);
            entity.Property(x => x.RemoteName).IsRequired();
            entity.Property(x => x.RemoteVersion).IsRequired();
            entity.Property(x => x.Status).IsRequired();
        });

        modelBuilder.Entity<ProjectEntity>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ShipName).IsRequired();
            entity.Property(x => x.JobName).IsRequired();
            entity.Property(x => x.RootPath).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.StructureVersion).IsRequired();
        });

        modelBuilder.Entity<ProjectFieldEntity>(entity =>
        {
            entity.ToTable("ProjectFields");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired();
            entity.Property(x => x.FieldType).IsRequired();
            entity.Property(x => x.Source).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
        });

        modelBuilder.Entity<DocumentCategoryEntity>(entity =>
        {
            entity.ToTable("DocumentCategories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.NormalizedName).IsRequired();
            entity.Property(x => x.Version).IsRequired();
            entity.HasIndex(x => x.NormalizedName).IsUnique();
            entity.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<CategoryChecklistItemEntity>(entity =>
        {
            entity.ToTable("CategoryChecklistItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).IsRequired();
            entity.HasIndex(x => x.CategoryId);
        });

        modelBuilder.Entity<MasterDocumentEntity>(entity =>
        {
            entity.ToTable("MasterDocuments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).IsRequired();
            entity.Property(x => x.SourceFileName).IsRequired();
            entity.Property(x => x.StoredFilePath).IsRequired();
            entity.Property(x => x.FileType).IsRequired();
            entity.Property(x => x.CurrentVersionNumber).IsRequired();
            entity.HasIndex(x => x.CategoryId);
            entity.HasIndex(x => x.IsArchived);
        });

        modelBuilder.Entity<MasterDocumentVersionEntity>(entity =>
        {
            entity.ToTable("MasterDocumentVersions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VersionNumber).IsRequired();
            entity.Property(x => x.StoredFilePath).IsRequired();
            entity.HasIndex(x => new { x.MasterDocumentId, x.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<ProjectCategoryEntity>(entity =>
        {
            entity.ToTable("ProjectCategories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.FolderPath).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.SortOrder });
        });

        modelBuilder.Entity<ProjectDocumentEntity>(entity =>
        {
            entity.ToTable("ProjectDocuments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).IsRequired();
            entity.Property(x => x.FilePath).IsRequired();
            entity.Property(x => x.FileType).IsRequired();
            entity.Property(x => x.Source).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.VersionSnapshot).IsRequired();
            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => x.ProjectCategoryId);
        });

        modelBuilder.Entity<DocumentFieldValueEntity>(entity =>
        {
            entity.ToTable("DocumentFieldValues");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired();
            entity.HasIndex(x => new { x.ProjectDocumentId, x.Key }).IsUnique();
        });

        modelBuilder.Entity<PlaceholderMappingEntity>(entity =>
        {
            entity.ToTable("PlaceholderMappings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlaceholderKey).IsRequired();
            entity.Property(x => x.ProfileFieldKey).IsRequired();
            entity.HasIndex(x => x.PlaceholderKey).IsUnique();
        });

        modelBuilder.Entity<BackupRecordEntity>(entity =>
        {
            entity.ToTable("BackupRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Location).IsRequired();
            entity.Property(x => x.Kind).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<SchemaVersionEntity>(entity =>
        {
            entity.ToTable("SchemaVersions");
            entity.HasKey(x => x.Version);
            entity.Property(x => x.Version).IsRequired();
        });
    }
}
