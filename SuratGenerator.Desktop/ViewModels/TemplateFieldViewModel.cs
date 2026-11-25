using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class TemplateFieldViewModel : ObservableObject
{
    private string _value = string.Empty;

    public event EventHandler? ValueChanged;

    public TemplateFieldViewModel(TemplateField field)
    {
        Key = field.Key;
        Label = field.Label;
        Position = field.Position;
        Kind = field.Kind;
    }

    public string Key { get; }

    public string Label { get; }

    public int Position { get; }

    public TemplateFieldKind Kind { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
