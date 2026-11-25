using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.Services;

internal interface IProjectDialogService
{
    DuplicateProjectDecision ShowDuplicate(ProjectDuplicateInfo duplicate);

    ProjectUploadCollisionPolicy ShowFileCollision(ProjectFileCollisionInfo collision);
}
