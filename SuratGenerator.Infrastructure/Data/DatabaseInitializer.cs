using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SuratGenerator.Infrastructure.Data;

internal static class DatabaseInitializer
{
    public static void Initialize(SuratGeneratorDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();
        ApplyPragmas(dbContext);
        dbContext.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Drafts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Drafts" PRIMARY KEY,
                "TemplateId" TEXT NOT NULL,
                "TemplateName" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "ValuesJson" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """);
        dbContext.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "GeneratedDocuments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_GeneratedDocuments" PRIMARY KEY,
                "TemplateId" TEXT NOT NULL,
                "TemplateName" TEXT NOT NULL,
                "DocumentName" TEXT NOT NULL,
                "Kind" INTEGER NOT NULL,
                "FilePath" TEXT NOT NULL,
                "FileSizeBytes" INTEGER NOT NULL,
                "GeneratedAt" TEXT NOT NULL
            );
            """);

        EnsureColumn(
            dbContext,
            "Category",
            "ALTER TABLE \"Templates\" ADD COLUMN \"Category\" TEXT NOT NULL DEFAULT 'Umum';");
        EnsureColumn(
            dbContext,
            "IsFavorite",
            "ALTER TABLE \"Templates\" ADD COLUMN \"IsFavorite\" INTEGER NOT NULL DEFAULT 0;");
        EnsureColumn(
            dbContext,
            "Version",
            "ALTER TABLE \"Templates\" ADD COLUMN \"Version\" TEXT NOT NULL DEFAULT '1.0';");
        EnsureColumn(
            dbContext,
            "SourceFilePath",
            "ALTER TABLE \"Templates\" ADD COLUMN \"SourceFilePath\" TEXT NULL;");
        EnsureColumn(
            dbContext,
            "SourceChecksum",
            "ALTER TABLE \"Templates\" ADD COLUMN \"SourceChecksum\" TEXT NULL;");
        EnsureColumn(
            dbContext,
            "SourceModifiedAt",
            "ALTER TABLE \"Templates\" ADD COLUMN \"SourceModifiedAt\" TEXT NULL;");
        dbContext.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "DriveSyncStates" (
                "TemplateId" TEXT NOT NULL CONSTRAINT "PK_DriveSyncStates" PRIMARY KEY,
                "RemoteFileId" TEXT NULL,
                "RemoteName" TEXT NOT NULL,
                "RemoteVersion" TEXT NOT NULL,
                "RemoteChecksum" TEXT NULL,
                "LocalChecksum" TEXT NULL,
                "RemoteModifiedAt" TEXT NULL,
                "LocalModifiedAt" TEXT NULL,
                "LastSyncedAt" TEXT NULL,
                "LastSyncedVersion" TEXT NULL,
                "Status" TEXT NOT NULL,
                "IsLocallyDeleted" INTEGER NOT NULL,
                "ErrorMessage" TEXT NULL
            );
            """);
        dbContext.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_Drafts_TemplateId" ON "Drafts" ("TemplateId");
            CREATE INDEX IF NOT EXISTS "IX_GeneratedDocuments_GeneratedAt" ON "GeneratedDocuments" ("GeneratedAt");
            CREATE INDEX IF NOT EXISTS "IX_DriveSyncStates_Status" ON "DriveSyncStates" ("Status");
            """);
        ApplyV31Schema(dbContext);
        ApplyV32Schema(dbContext);
        ApplyV33Schema(dbContext);
        ApplyV34Schema(dbContext);
        ApplyV35Schema(dbContext);
    }

    private static void ApplyV31Schema(SuratGeneratorDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SchemaVersions" (
                "Version" TEXT NOT NULL CONSTRAINT "PK_SchemaVersions" PRIMARY KEY,
                "AppliedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "Projects" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Projects" PRIMARY KEY,
                "ShipName" TEXT NOT NULL,
                "JobName" TEXT NOT NULL,
                "RootPath" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "StructureVersion" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "ProjectFields" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ProjectFields" PRIMARY KEY,
                "ProjectId" TEXT NOT NULL,
                "Key" TEXT NOT NULL,
                "Value" TEXT NOT NULL,
                "FieldType" TEXT NOT NULL,
                "Source" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "DocumentCategories" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentCategories" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "NormalizedName" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "Version" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "MasterDocuments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_MasterDocuments" PRIMARY KEY,
                "CategoryId" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "SourceFileName" TEXT NOT NULL,
                "StoredFilePath" TEXT NOT NULL,
                "FileType" TEXT NOT NULL,
                "IsRequired" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsArchived" INTEGER NOT NULL,
                "CurrentVersionId" TEXT NULL,
                "CurrentVersionNumber" TEXT NOT NULL,
                "Checksum" TEXT NULL,
                "FileSizeBytes" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "MasterDocumentVersions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_MasterDocumentVersions" PRIMARY KEY,
                "MasterDocumentId" TEXT NOT NULL,
                "VersionNumber" TEXT NOT NULL,
                "StoredFilePath" TEXT NOT NULL,
                "Checksum" TEXT NULL,
                "FileSizeBytes" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "ProjectCategories" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ProjectCategories" PRIMARY KEY,
                "ProjectId" TEXT NOT NULL,
                "SourceCategoryId" TEXT NULL,
                "Name" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "FolderPath" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "ProjectDocuments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ProjectDocuments" PRIMARY KEY,
                "ProjectId" TEXT NOT NULL,
                "ProjectCategoryId" TEXT NOT NULL,
                "MasterDocumentId" TEXT NULL,
                "MasterVersionId" TEXT NULL,
                "DisplayName" TEXT NOT NULL,
                "FilePath" TEXT NOT NULL,
                "FileType" TEXT NOT NULL,
                "Source" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "VersionSnapshot" TEXT NOT NULL,
                "IsRequired" INTEGER NOT NULL DEFAULT 0,
                "FileSizeBytes" INTEGER NOT NULL DEFAULT 0,
                "LastModifiedUtc" TEXT NULL,
                "ImportedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "DocumentFieldValues" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentFieldValues" PRIMARY KEY,
                "ProjectDocumentId" TEXT NOT NULL,
                "Key" TEXT NOT NULL,
                "Value" TEXT NOT NULL,
                "IsOverride" INTEGER NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "PlaceholderMappings" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PlaceholderMappings" PRIMARY KEY,
                "PlaceholderKey" TEXT NOT NULL,
                "ProfileFieldKey" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "BackupRecords" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_BackupRecords" PRIMARY KEY,
                "CreatedAt" TEXT NOT NULL,
                "Location" TEXT NOT NULL,
                "Kind" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "IsVerified" INTEGER NOT NULL,
                "ItemCount" INTEGER NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "Message" TEXT NULL
            );
            """);
        dbContext.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProjectFields_ProjectId_Key" ON "ProjectFields" ("ProjectId", "Key");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentCategories_NormalizedName" ON "DocumentCategories" ("NormalizedName");
            CREATE INDEX IF NOT EXISTS "IX_DocumentCategories_SortOrder" ON "DocumentCategories" ("SortOrder");
            CREATE INDEX IF NOT EXISTS "IX_MasterDocuments_CategoryId" ON "MasterDocuments" ("CategoryId");
            CREATE INDEX IF NOT EXISTS "IX_MasterDocuments_IsArchived" ON "MasterDocuments" ("IsArchived");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MasterDocumentVersions_MasterDocumentId_VersionNumber" ON "MasterDocumentVersions" ("MasterDocumentId", "VersionNumber");
            CREATE INDEX IF NOT EXISTS "IX_ProjectCategories_ProjectId_SortOrder" ON "ProjectCategories" ("ProjectId", "SortOrder");
            CREATE INDEX IF NOT EXISTS "IX_ProjectDocuments_ProjectId" ON "ProjectDocuments" ("ProjectId");
            CREATE INDEX IF NOT EXISTS "IX_ProjectDocuments_ProjectCategoryId" ON "ProjectDocuments" ("ProjectCategoryId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentFieldValues_ProjectDocumentId_Key" ON "DocumentFieldValues" ("ProjectDocumentId", "Key");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlaceholderMappings_PlaceholderKey" ON "PlaceholderMappings" ("PlaceholderKey");
            CREATE INDEX IF NOT EXISTS "IX_BackupRecords_CreatedAt" ON "BackupRecords" ("CreatedAt");
            """);
        dbContext.Database.ExecuteSqlRaw(
            "INSERT OR IGNORE INTO \"SchemaVersions\" (\"Version\", \"AppliedAt\") VALUES ({0}, {1});",
            "v3.1",
            DateTimeOffset.UtcNow.ToString("o"));
    }

    private static void ApplyV32Schema(SuratGeneratorDbContext dbContext)
    {
        EnsureColumn(
            dbContext,
            "ProjectDocuments",
            "IsRequired",
            "ALTER TABLE \"ProjectDocuments\" ADD COLUMN \"IsRequired\" INTEGER NOT NULL DEFAULT 0;");
        EnsureColumn(
            dbContext,
            "ProjectDocuments",
            "FileSizeBytes",
            "ALTER TABLE \"ProjectDocuments\" ADD COLUMN \"FileSizeBytes\" INTEGER NOT NULL DEFAULT 0;");
        EnsureColumn(
            dbContext,
            "ProjectDocuments",
            "LastModifiedUtc",
            "ALTER TABLE \"ProjectDocuments\" ADD COLUMN \"LastModifiedUtc\" TEXT NULL;");
        dbContext.Database.ExecuteSqlRaw(
            "INSERT OR IGNORE INTO \"SchemaVersions\" (\"Version\", \"AppliedAt\") VALUES ({0}, {1});",
            "v3.2",
            DateTimeOffset.UtcNow.ToString("o"));
    }

    private static void ApplyV33Schema(SuratGeneratorDbContext dbContext)
    {
        EnsureColumn(
            dbContext,
            "ProjectDocuments",
            "BaseTemplateFilePath",
            "ALTER TABLE \"ProjectDocuments\" ADD COLUMN \"BaseTemplateFilePath\" TEXT NULL;");
        EnsureColumn(
            dbContext,
            "ProjectDocuments",
            "EditedOutputFilePath",
            "ALTER TABLE \"ProjectDocuments\" ADD COLUMN \"EditedOutputFilePath\" TEXT NULL;");
        EnsureColumn(
            dbContext,
            "ProjectDocuments",
            "LastEditedAt",
            "ALTER TABLE \"ProjectDocuments\" ADD COLUMN \"LastEditedAt\" TEXT NULL;");
        dbContext.Database.ExecuteSqlRaw(
            "INSERT OR IGNORE INTO \"SchemaVersions\" (\"Version\", \"AppliedAt\") VALUES ({0}, {1});",
            "v3.3",
            DateTimeOffset.UtcNow.ToString("o"));
    }

    private static void ApplyV34Schema(SuratGeneratorDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "CategoryChecklistItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CategoryChecklistItems" PRIMARY KEY,
                "CategoryId" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_CategoryChecklistItems_CategoryId" ON "CategoryChecklistItems" ("CategoryId");
            """);
        dbContext.Database.ExecuteSqlRaw(
            "INSERT OR IGNORE INTO \"SchemaVersions\" (\"Version\", \"AppliedAt\") VALUES ({0}, {1});",
            "v3.4",
            DateTimeOffset.UtcNow.ToString("o"));
    }

    private static void ApplyV35Schema(SuratGeneratorDbContext dbContext)
    {
        EnsureColumn(
            dbContext,
            "Projects",
            "IsMissing",
            "ALTER TABLE \"Projects\" ADD COLUMN \"IsMissing\" INTEGER NOT NULL DEFAULT 0;");
        dbContext.Database.ExecuteSqlRaw(
            "INSERT OR IGNORE INTO \"SchemaVersions\" (\"Version\", \"AppliedAt\") VALUES ({0}, {1});",
            "v3.5",
            DateTimeOffset.UtcNow.ToString("o"));
    }

    private static void ApplyPragmas(SuratGeneratorDbContext dbContext)
    {
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=5000;
            PRAGMA foreign_keys=ON;
            PRAGMA synchronous=NORMAL;
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureColumn(
        SuratGeneratorDbContext dbContext,
        string column,
        string alterStatement) =>
        EnsureColumn(dbContext, "Templates", column, alterStatement);

    private static void EnsureColumn(
        SuratGeneratorDbContext dbContext,
        string table,
        string column,
        string alterStatement)
    {
        var exists = dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
            .Single() > 0;

        if (!exists)
        {
            dbContext.Database.ExecuteSqlRaw(alterStatement);
        }
    }
}
