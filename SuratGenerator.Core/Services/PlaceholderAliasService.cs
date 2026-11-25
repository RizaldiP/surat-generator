using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public sealed class PlaceholderAliasService : IPlaceholderAliasService
{
    private static readonly Dictionary<string, string> BuiltInAliases = new(StringComparer.Ordinal)
    {
        ["kapal"] = ProfileFieldKeys.ShipName,
        ["namakapal"] = ProfileFieldKeys.ShipName,
        ["namavessel"] = ProfileFieldKeys.ShipName,
        ["vessel"] = ProfileFieldKeys.ShipName,
        ["pekerjaan"] = ProfileFieldKeys.JobName,
        ["namapekerjaan"] = ProfileFieldKeys.JobName,
        ["job"] = ProfileFieldKeys.JobName,
        ["jobname"] = ProfileFieldKeys.JobName,
        ["kontrak"] = ProfileFieldKeys.ContractNumber,
        ["nomorkontrak"] = ProfileFieldKeys.ContractNumber,
        ["nokontrak"] = ProfileFieldKeys.ContractNumber,
        ["tanggalpekerjaan"] = ProfileFieldKeys.ContractDate,
        ["nilaikontrak"] = ProfileFieldKeys.ContractValue,
        ["nilai"] = ProfileFieldKeys.ContractValue,
        ["perusahaan"] = ProfileFieldKeys.Company,
        ["namaperusahaan"] = ProfileFieldKeys.Company,
        ["company"] = ProfileFieldKeys.Company,
        ["penerima"] = ProfileFieldKeys.Recipient,
        ["recipient"] = ProfileFieldKeys.Recipient,
        ["lokasi"] = ProfileFieldKeys.WorkLocation,
        ["lokasipekerjaan"] = ProfileFieldKeys.WorkLocation,
        ["worklocation"] = ProfileFieldKeys.WorkLocation
    };

    public string NormalizeKey(string value)
    {
        var text = (value ?? string.Empty).Trim().Trim('{', '}').Trim();
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    public string ResolveProfileFieldKey(
        string key,
        IReadOnlyList<PlaceholderMappingInfo>? mappings = null)
    {
        var normalized = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return key ?? string.Empty;
        }

        foreach (var mapping in mappings ?? Array.Empty<PlaceholderMappingInfo>())
        {
            if (mapping.IsActive
                && string.Equals(NormalizeKey(mapping.ProfileFieldKey), normalized, StringComparison.Ordinal))
            {
                return mapping.ProfileFieldKey.Trim();
            }
        }

        foreach (var profileKey in BuiltInAliases.Values)
        {
            if (string.Equals(NormalizeKey(profileKey), normalized, StringComparison.Ordinal))
            {
                return profileKey;
            }
        }

        return key.Trim();
    }

    public IReadOnlyList<DocumentFieldEditField> BuildFields(
        IReadOnlyList<TemplateField> templateFields,
        IReadOnlyList<ProjectFieldInfo> profileFields,
        IReadOnlyList<DocumentFieldValueInfo> savedValues,
        IReadOnlyList<PlaceholderMappingInfo> mappings)
    {
        if (templateFields is null)
        {
            throw new ArgumentNullException(nameof(templateFields));
        }

        var aliases = BuildAliases(mappings);
        var profileValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in profileFields ?? Array.Empty<ProjectFieldInfo>())
        {
            var key = NormalizeKey(field.Key);
            if (!string.IsNullOrWhiteSpace(key))
            {
                profileValues[key] = field.Value;
            }
        }

        var saved = new Dictionary<string, DocumentFieldValueInfo>(StringComparer.Ordinal);
        foreach (var value in savedValues ?? Array.Empty<DocumentFieldValueInfo>())
        {
            var key = NormalizeKey(value.Key);
            if (!string.IsNullOrWhiteSpace(key))
            {
                saved[key] = value;
            }
        }

        var result = new List<DocumentFieldEditField>(templateFields.Count);
        foreach (var field in templateFields)
        {
            var placeholderKey = NormalizeKey(field.Key);
            if (string.IsNullOrWhiteSpace(placeholderKey))
            {
                continue;
            }

            string? profileKey = null;
            if (aliases.TryGetValue(placeholderKey, out var mapped))
            {
                profileKey = mapped;
            }
            else if (BuiltInAliases.TryGetValue(placeholderKey, out var builtIn))
            {
                profileKey = builtIn;
            }

            var canonicalKey = NormalizeKey(profileKey ?? placeholderKey);
            var hasSaved = saved.TryGetValue(canonicalKey, out var savedValue);
            string? profileValue = null;
            var hasProfile = profileKey is not null
                && profileValues.TryGetValue(canonicalKey, out profileValue);
            var value = hasSaved
                ? savedValue!.Value
                : hasProfile
                    ? profileValue ?? string.Empty
                    : string.Empty;

            result.Add(new DocumentFieldEditField(
                field.Key,
                canonicalKey,
                field.Label,
                value,
                profileKey,
                hasSaved && savedValue!.IsOverride));
        }

        return result;
    }

    public IReadOnlyDictionary<string, string> BuildValues(
        IReadOnlyList<DocumentFieldEditField> fields)
    {
        if (fields is null)
        {
            throw new ArgumentNullException(nameof(fields));
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            values[field.Key] = field.Value ?? string.Empty;
        }

        return values;
    }

    public IReadOnlyList<string> FindUnfilledPlaceholders(
        IReadOnlyList<DocumentFieldEditField> fields)
    {
        if (fields is null)
        {
            throw new ArgumentNullException(nameof(fields));
        }

        return fields
            .Where(field => string.IsNullOrWhiteSpace(field.Value))
            .Select(field => field.Key)
            .ToArray();
    }

    private Dictionary<string, string> BuildAliases(IReadOnlyList<PlaceholderMappingInfo>? mappings)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mapping in mappings ?? Array.Empty<PlaceholderMappingInfo>())
        {
            if (!mapping.IsActive)
            {
                continue;
            }

            var placeholder = NormalizeKey(mapping.PlaceholderKey);
            var profile = NormalizeKey(mapping.ProfileFieldKey);
            if (!string.IsNullOrWhiteSpace(placeholder) && !string.IsNullOrWhiteSpace(profile))
            {
                aliases[placeholder] = profile;
            }
        }

        return aliases;
    }
}
