using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.Services;

internal interface IConfirmationService
{
    bool ConfirmReset();

    bool ConfirmDeleteTemplate(string templateName);

    bool ConfirmDeactivateCategory(string categoryName);

    bool ConfirmDeleteCategory(string categoryName);

    bool ConfirmReplaceMaster(string displayName);

    bool ConfirmDeactivateMaster(string displayName);

    bool ConfirmArchiveMaster(string displayName);

    bool ConfirmRelocateMaster(string oldPath, string newPath);

    bool ConfirmPromoteField(string key);

    bool ConfirmRefreshProfileFields();

    bool ConfirmReplaceOutput(string path);

    bool ConfirmDeleteChecklistItem(string displayName);

    bool ConfirmDeleteProject(string shipName, string jobName);

    bool ConfirmDeleteDocument(string displayName, bool hasFile);

    bool ConfirmToggleChecklistItem(string displayName, bool nextActive);

    bool ConfirmMarkComplete(string displayName);

    bool ConfirmRestore(BackupPreview preview);
}
