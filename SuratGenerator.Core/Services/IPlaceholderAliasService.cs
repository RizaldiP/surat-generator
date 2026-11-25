using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IPlaceholderAliasService
{
    string NormalizeKey(string value);

    string ResolveProfileFieldKey(string key, IReadOnlyList<PlaceholderMappingInfo>? mappings = null);

    IReadOnlyList<DocumentFieldEditField> BuildFields(
        IReadOnlyList<TemplateField> templateFields,
        IReadOnlyList<ProjectFieldInfo> profileFields,
        IReadOnlyList<DocumentFieldValueInfo> savedValues,
        IReadOnlyList<PlaceholderMappingInfo> mappings);

    IReadOnlyDictionary<string, string> BuildValues(
        IReadOnlyList<DocumentFieldEditField> fields);

    IReadOnlyList<string> FindUnfilledPlaceholders(
        IReadOnlyList<DocumentFieldEditField> fields);
}
