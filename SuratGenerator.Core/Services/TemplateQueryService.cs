using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public static class TemplateQueryService
{
    public static IReadOnlyList<TemplateInfo> Query(
        IEnumerable<TemplateInfo> templates,
        string searchText,
        string category,
        TemplateSort sort,
        bool favoritesOnly = false)
    {
        var search = searchText?.Trim() ?? string.Empty;
        var selectedCategory = category?.Trim() ?? string.Empty;

        var results = templates.Where(template =>
        {
            if (favoritesOnly && !template.IsFavorite)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(selectedCategory)
                && !string.Equals(selectedCategory, "Semua", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(template.Category, selectedCategory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (search.Length == 0)
            {
                return true;
            }

            return Contains(template.Name, search)
                || Contains(template.SourceFileName, search)
                || Contains(template.Category, search);
        });

        return sort switch
        {
            TemplateSort.NameDescending => results
                .OrderByDescending(template => template.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            TemplateSort.Newest => results
                .OrderByDescending(template => template.ImportedAt)
                .ToArray(),
            TemplateSort.Oldest => results
                .OrderBy(template => template.ImportedAt)
                .ToArray(),
            _ => results
                .OrderBy(template => template.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray()
        };
    }

    private static bool Contains(string value, string search)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
