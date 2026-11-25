using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.Messages;

internal sealed record TemplatesChangedMessage(TemplateFolderScanSummary Summary);
