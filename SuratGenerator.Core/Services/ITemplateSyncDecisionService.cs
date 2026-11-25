namespace SuratGenerator.Core.Services;

using SuratGenerator.Core.Models;

public interface ITemplateSyncDecisionService
{
    TemplateSyncDecision Decide(TemplateSyncSnapshot snapshot);
}
