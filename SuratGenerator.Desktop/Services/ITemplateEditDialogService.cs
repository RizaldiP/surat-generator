using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.Services;

internal interface ITemplateEditDialogService
{
    TemplateEditResult? Show(TemplateInfo template);
}
