using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class MasterDocumentStore : IMasterDocumentStore, IDisposable
{
    private const int MaximumDisplayNameLength = 120;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".doc", ".xlsx", ".xls", ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".gif"
    };

    private readonly SuratGeneratorDbContext _dbContext;
    private readonly IWorkspacePathService _pathService;
    private readonly IWorkspaceFileService _fileService;
    private readonly IAppLogger _logger;

    public MasterDocumentStore(
        IAppLogger? logger = null,
        IWorkspacePathService? pathService = null,
        IWorkspaceFileService? fileService = null)
        : this(
            DatabaseOptionsFactory.CreateProduction(),
            pathService ?? new WorkspacePathService(),
            fileService ?? new WorkspaceFileService(),
            logger)
    {
    }

    public MasterDocumentStore(
        DbContextOptions<SuratGeneratorDbContext> options,
        IWorkspacePathService pathService,
        IWorkspaceFileService? fileService = null,
        IAppLogger? logger = null)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        _pathService = pathService;
        _fileService = fileService ?? new WorkspaceFileService();
        _logger = logger ?? NullAppLogger.Instance;
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<IReadOnlyList<MasterDocumentInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.DocumentCategories
            .AsNoTracking()
            .ToDictionaryAsync(category => category.Id, cancellationToken);
        var documents = await _dbContext.MasterDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return documents
            .OrderBy(document => categories.TryGetValue(document.CategoryId, out var category) ? category.SortOrder : int.MaxValue)
            .ThenBy(document => document.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyList<MasterDocumentInfo>> GetByCategoryAsync(
        Guid categoryId,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MasterDocuments
            .AsNoTracking()
            .Where(document => document.CategoryId == categoryId);
        if (!includeArchived)
        {
            query = query.Where(document => !document.IsArchived);
        }

        var documents = await query.ToListAsync(cancellationToken);
        return documents
            .OrderBy(document => document.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyList<MasterDocumentVersionInfo>> GetVersionsAsync(
        Guid masterDocumentId,
        CancellationToken cancellationToken = default)
    {
        var versions = await _dbContext.MasterDocumentVersions
            .AsNoTracking()
            .Where(version => version.MasterDocumentId == masterDocumentId)
            .ToListAsync(cancellationToken);

        return versions
            .OrderByDescending(version => ParseVersion(version.VersionNumber))
            .Select(Map)
            .ToArray();
    }

    public async Task<MasterDocumentInfo> UploadAsync(
        Guid categoryId,
        string sourcePath,
        string displayName,
        bool isRequired,
        CancellationToken cancellationToken = default)
    {
        var extension = ValidateSourceFile(sourcePath);
        var category = await EnsureCategoryAsync(categoryId, cancellationToken);
        var root = await GetRootAsync(cancellationToken);
        var name = NormalizeDisplayName(string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : displayName);
        await EnsureUniqueDisplayNameAsync(categoryId, name, null, cancellationToken);

        var masterId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var versionNumber = "1.0";
        var relativePath = BuildRelativePath(categoryId, masterId, versionId, extension);
        var storedPath = await _fileService.CopyAsync(sourcePath, root, relativePath, false, cancellationToken);
        var checksum = await _fileService.ComputeChecksumAsync(storedPath, cancellationToken);
        var size = new FileInfo(storedPath).Length;
        var now = DateTimeOffset.UtcNow;

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var version = new MasterDocumentVersionEntity
            {
                Id = versionId,
                MasterDocumentId = masterId,
                VersionNumber = versionNumber,
                StoredFilePath = relativePath,
                Checksum = checksum,
                FileSizeBytes = size,
                CreatedAt = now
            };
            var master = new MasterDocumentEntity
            {
                Id = masterId,
                CategoryId = category.Id,
                DisplayName = name,
                SourceFileName = Path.GetFileName(sourcePath),
                StoredFilePath = relativePath,
                FileType = extension,
                IsRequired = isRequired,
                IsActive = true,
                IsArchived = false,
                CurrentVersionId = versionId,
                CurrentVersionNumber = versionNumber,
                Checksum = checksum,
                FileSizeBytes = size,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.MasterDocumentVersions.Add(version);
            _dbContext.MasterDocuments.Add(master);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(master);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            TryDeleteFile(storedPath);
            throw;
        }
    }

    public async Task<MasterDocumentInfo> ReplaceVersionAsync(
        Guid masterDocumentId,
        string sourcePath,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var extension = ValidateSourceFile(sourcePath);
        var master = await FindEntityAsync(masterDocumentId, cancellationToken);
        var name = displayName is null
            ? master.DisplayName
            : NormalizeDisplayName(displayName);
        await EnsureUniqueDisplayNameAsync(master.CategoryId, name, master.Id, cancellationToken);

        var versionNumber = NextVersion(master.CurrentVersionNumber);
        var versionId = Guid.NewGuid();
        var root = await GetRootAsync(cancellationToken);
        var relativePath = BuildRelativePath(master.CategoryId, master.Id, versionId, extension);
        var storedPath = await _fileService.CopyAsync(sourcePath, root, relativePath, false, cancellationToken);
        var checksum = await _fileService.ComputeChecksumAsync(storedPath, cancellationToken);
        var size = new FileInfo(storedPath).Length;
        var now = DateTimeOffset.UtcNow;

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var version = new MasterDocumentVersionEntity
            {
                Id = versionId,
                MasterDocumentId = master.Id,
                VersionNumber = versionNumber,
                StoredFilePath = relativePath,
                Checksum = checksum,
                FileSizeBytes = size,
                CreatedAt = now
            };

            _dbContext.MasterDocumentVersions.Add(version);
            master.DisplayName = name;
            master.SourceFileName = Path.GetFileName(sourcePath);
            master.StoredFilePath = relativePath;
            master.FileType = extension;
            master.CurrentVersionId = versionId;
            master.CurrentVersionNumber = versionNumber;
            master.Checksum = checksum;
            master.FileSizeBytes = size;
            master.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(master);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            TryDeleteFile(storedPath);
            throw;
        }
    }

    public async Task<MasterDocumentInfo> UpdateMetadataAsync(
        Guid id,
        string? displayName = null,
        bool? isRequired = null,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var master = await FindEntityAsync(id, cancellationToken);
        var name = displayName is null ? master.DisplayName : NormalizeDisplayName(displayName);
        var targetCategoryId = categoryId ?? master.CategoryId;
        await EnsureUniqueDisplayNameAsync(targetCategoryId, name, master.Id, cancellationToken);
        if (targetCategoryId != master.CategoryId)
        {
            await EnsureCategoryAsync(targetCategoryId, cancellationToken);
        }

        string? oldDirectory = null;
        string? newDirectory = null;
        var versions = await _dbContext.MasterDocumentVersions
            .Where(version => version.MasterDocumentId == id)
            .ToListAsync(cancellationToken);

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (targetCategoryId != master.CategoryId)
            {
                var root = await GetRootAsync(cancellationToken);
                oldDirectory = Path.Combine(master.CategoryId.ToString("N"), master.Id.ToString("N"));
                newDirectory = Path.Combine(targetCategoryId.ToString("N"), master.Id.ToString("N"));
                var oldAbsolute = Path.GetFullPath(Path.Combine(root, oldDirectory));
                var newAbsolute = Path.GetFullPath(Path.Combine(root, newDirectory));
                ValidateContainment(root, oldAbsolute);
                ValidateContainment(root, newAbsolute);

                if (Directory.Exists(newAbsolute))
                {
                    throw new IOException($"Folder tujuan kategori sudah ada: {newAbsolute}");
                }

                if (Directory.Exists(oldAbsolute))
                {
                    var parent = Path.GetDirectoryName(newAbsolute);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    Directory.Move(oldAbsolute, newAbsolute);
                }

                foreach (var version in versions)
                {
                    version.StoredFilePath = ReplaceDirectory(version.StoredFilePath, oldDirectory, newDirectory);
                }

                master.StoredFilePath = ReplaceDirectory(master.StoredFilePath, oldDirectory, newDirectory);
            }

            master.DisplayName = name;
            master.CategoryId = targetCategoryId;
            if (isRequired.HasValue)
            {
                master.IsRequired = isRequired.Value;
            }

            master.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(master);
        }
        catch
        {
            if (oldDirectory is not null && newDirectory is not null)
            {
                var root = await GetRootAsync(cancellationToken);
                var oldAbsolute = Path.GetFullPath(Path.Combine(root, oldDirectory));
                var newAbsolute = Path.GetFullPath(Path.Combine(root, newDirectory));
                if (Directory.Exists(newAbsolute) && !Directory.Exists(oldAbsolute))
                {
                    Directory.Move(newAbsolute, oldAbsolute);
                }
            }

            await transaction.RollbackAsync(cancellationToken);
            await _dbContext.Entry(master).ReloadAsync(cancellationToken);
            foreach (var version in versions)
            {
                await _dbContext.Entry(version).ReloadAsync(cancellationToken);
            }

            throw;
        }
    }

    public async Task<MasterDocumentInfo> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var master = await FindEntityAsync(id, cancellationToken);
        master.IsActive = isActive;
        master.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(master);
    }

    public async Task<MasterDocumentInfo> SetArchivedAsync(
        Guid id,
        bool isArchived,
        CancellationToken cancellationToken = default)
    {
        var master = await FindEntityAsync(id, cancellationToken);
        master.IsArchived = isArchived;
        if (isArchived)
        {
            master.IsActive = false;
        }

        master.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(master);
    }

    public async Task<MasterDocumentInfo> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var master = await FindEntityAsync(id, cancellationToken);
        master.IsArchived = false;
        master.IsActive = true;
        master.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(master);
    }

    public async Task<string> ResolveOpenPathAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var master = await _dbContext.MasterDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Master dokumen tidak ditemukan.");
        var root = await GetRootAsync(cancellationToken);
        var path = ResolvePath(root, master.StoredFilePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File master tidak ditemukan.", path);
        }

        return path;
    }

    public async Task RelocateAsync(
        string oldRoot,
        string newRoot,
        CancellationToken cancellationToken = default)
    {
        var sourceRoot = PathSafety.NormalizeRoot(oldRoot);
        var destinationRoot = PathSafety.NormalizeRoot(newRoot);
        if (string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(destinationRoot);

        _dbContext.ChangeTracker.Clear();
        var versions = await _dbContext.MasterDocumentVersions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var masters = await _dbContext.MasterDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var moves = new List<(string Source, string Destination)>();

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var version in versions)
            {
                var relative = ToRelativePath(sourceRoot, version.StoredFilePath);
                version.StoredFilePath = relative;
                AddMove(moves, sourceRoot, destinationRoot, relative);
            }

            foreach (var master in masters)
            {
                var relative = ToRelativePath(sourceRoot, master.StoredFilePath);
                master.StoredFilePath = relative;
                AddMove(moves, sourceRoot, destinationRoot, relative);
            }

            for (var index = 0; index < moves.Count; index++)
            {
                var move = moves[index];
                if (!File.Exists(move.Source))
                {
                    throw new FileNotFoundException("File master tidak ditemukan saat memindah lokasi.", move.Source);
                }

                var parent = Path.GetDirectoryName(move.Destination);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                if (File.Exists(move.Destination))
                {
                    throw new IOException($"File tujuan sudah ada: {move.Destination}");
                }

                File.Move(move.Source, move.Destination);
            }

            _dbContext.MasterDocumentVersions.UpdateRange(versions);
            _dbContext.MasterDocuments.UpdateRange(masters);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            for (var index = moves.Count - 1; index >= 0; index--)
            {
                TryRestoreMove(moves[index].Source, moves[index].Destination);
            }

            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task<DocumentCategoryEntity> EnsureCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var category = await _dbContext.DocumentCategories
            .FirstOrDefaultAsync(item => item.Id == categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Kategori dokumen tidak ditemukan.");
        if (!category.IsActive)
        {
            throw new InvalidOperationException("Kategori dokumen tidak aktif.");
        }

        return category;
    }

    private async Task<MasterDocumentEntity> FindEntityAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await _dbContext.MasterDocuments.FirstOrDefaultAsync(document => document.Id == id, cancellationToken)
        ?? throw new InvalidOperationException("Master dokumen tidak ditemukan.");

    private async Task EnsureUniqueDisplayNameAsync(
        Guid categoryId,
        string displayName,
        Guid? ignoreId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.MasterDocuments
            .AsNoTracking()
            .Where(document => document.CategoryId == categoryId);
        if (ignoreId.HasValue)
        {
            query = query.Where(document => document.Id != ignoreId.Value);
        }

        var names = await query.Select(document => document.DisplayName).ToListAsync(cancellationToken);
        if (names.Any(name => string.Equals(name, displayName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Nama master dokumen sudah digunakan pada kategori ini.", nameof(displayName));
        }
    }

    private async Task<string> GetRootAsync(CancellationToken cancellationToken)
    {
        await _pathService.EnsureDirectoriesAsync(cancellationToken);
        var paths = await _pathService.GetAsync(cancellationToken);
        return PathSafety.NormalizeRoot(paths.MasterDocumentsDirectory);
    }

    private static string ValidateSourceFile(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("File sumber tidak ditemukan.", sourcePath);
        }

        var extension = Path.GetExtension(sourcePath);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"Tipe file '{extension}' tidak diizinkan untuk master dokumen.", nameof(sourcePath));
        }

        return extension;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Nama tampilan tidak boleh kosong.", nameof(displayName));
        }

        var text = displayName.Trim();
        if (text.Length > MaximumDisplayNameLength)
        {
            throw new ArgumentException($"Nama tampilan maksimal {MaximumDisplayNameLength} karakter.", nameof(displayName));
        }

        if (PathSafety.HasInvalidFileNameChars(text)
            || PathSafety.HasTrailingDotOrSpace(text)
            || PathSafety.IsReservedName(text))
        {
            throw new ArgumentException("Nama tampilan mengandung karakter yang tidak diizinkan.", nameof(displayName));
        }

        return text;
    }

    private static string BuildRelativePath(Guid categoryId, Guid masterId, Guid versionId, string extension) =>
        Path.Combine(categoryId.ToString("N"), masterId.ToString("N"), $"{versionId:N}{extension}");

    private static string NextVersion(string current)
    {
        var parts = current.Split('.');
        if (parts.Length == 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
        {
            return $"{major}.{minor + 1}";
        }

        return "1.1";
    }

    private static int ParseVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
        {
            return (major * 1000) + minor;
        }

        return 0;
    }

    private static void ValidateContainment(string root, string path)
    {
        if (!PathSafety.IsWithin(root, path, false))
        {
            throw new ArgumentException("Path master berada di luar lokasi master yang diizinkan.");
        }
    }

    private static string ResolvePath(string root, string storedPath)
    {
        var path = Path.IsPathRooted(storedPath)
            ? Path.GetFullPath(storedPath)
            : Path.GetFullPath(Path.Combine(root, storedPath));
        ValidateContainment(root, path);
        return path;
    }

    private static string ToRelativePath(string root, string storedPath)
    {
        if (!Path.IsPathRooted(storedPath))
        {
            return storedPath;
        }

        var absolute = Path.GetFullPath(storedPath);
        ValidateContainment(root, absolute);
        return Path.GetRelativePath(root, absolute);
    }

    private static string ReplaceDirectory(string storedPath, string oldDirectory, string newDirectory)
    {
        var fileName = Path.GetFileName(storedPath);
        return Path.Combine(newDirectory, fileName);
    }

    private static void AddMove(
        List<(string Source, string Destination)> moves,
        string sourceRoot,
        string destinationRoot,
        string relativePath)
    {
        var source = Path.GetFullPath(Path.Combine(sourceRoot, relativePath));
        var destination = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
        if (!moves.Any(move => string.Equals(move.Source, source, StringComparison.OrdinalIgnoreCase)))
        {
            moves.Add((source, destination));
        }
    }

    private void TryDeleteFile(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }

        _logger.Warning($"Gagal menghapus file sementara master: {path}");
    }

    private static void TryRestoreMove(string source, string destination)
    {
        try
        {
            if (File.Exists(destination) && !File.Exists(source))
            {
                var parent = Path.GetDirectoryName(source);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.Move(destination, source);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static MasterDocumentInfo Map(MasterDocumentEntity entity) =>
        new(
            entity.Id,
            entity.CategoryId,
            entity.DisplayName,
            entity.SourceFileName,
            entity.StoredFilePath,
            entity.FileType,
            entity.IsRequired,
            entity.IsActive,
            entity.IsArchived,
            entity.CurrentVersionId,
            entity.CurrentVersionNumber,
            entity.Checksum,
            entity.FileSizeBytes,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static MasterDocumentVersionInfo Map(MasterDocumentVersionEntity entity) =>
        new(
            entity.Id,
            entity.MasterDocumentId,
            entity.VersionNumber,
            entity.StoredFilePath,
            entity.Checksum,
            entity.FileSizeBytes,
            entity.CreatedAt);
}
