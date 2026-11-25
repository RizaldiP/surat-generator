using Microsoft.EntityFrameworkCore;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public static class DatabaseOptionsFactory
{
    public static DbContextOptions<SuratGeneratorDbContext> CreateProduction()
    {
        var databasePath = AppPaths.DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        return new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
    }
}
