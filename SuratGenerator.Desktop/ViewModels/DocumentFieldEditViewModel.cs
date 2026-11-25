using System.ComponentModel;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class DocumentFieldEditViewModel : INotifyPropertyChanged
{
    private string _value;

    public DocumentFieldEditViewModel(Core.Models.DocumentFieldEditField field)
    {
        Key = field.Key;
        CanonicalKey = field.CanonicalKey;
        Label = field.Label;
        ProfileFieldKey = field.ProfileFieldKey;
        IsOverride = field.IsOverride;
        _value = field.Value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string CanonicalKey { get; }

    public string Label { get; }

    public string? ProfileFieldKey { get; }

    public bool IsOverride { get; private set; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value ?? string.Empty;
            IsOverride = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOverride)));
        }
    }

    public void SetValue(string value)
    {
        if (_value == value)
        {
            return;
        }

        _value = value ?? string.Empty;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }

    public Core.Models.DocumentFieldEditField ToModel() =>
        new(Key, CanonicalKey, Label, Value, ProfileFieldKey, IsOverride);
}
