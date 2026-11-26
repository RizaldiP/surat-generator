namespace SuratGenerator.Desktop.Services;

internal interface INoteDialogService
{
    NoteEditResult? ShowNote(string? existingTitle);

    NoteItemEditResult? ShowItem(string? existingName, string? existingDescription);
}
