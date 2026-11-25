using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace SuratGenerator.Desktop.Views.Controls;

public partial class EmptyStateControl : UserControl
{
    public static readonly DependencyProperty TitleTextProperty = DependencyProperty.Register(
        nameof(TitleText),
        typeof(string),
        typeof(EmptyStateControl),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageTextProperty = DependencyProperty.Register(
        nameof(MessageText),
        typeof(string),
        typeof(EmptyStateControl),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(SymbolRegular),
        typeof(EmptyStateControl),
        new PropertyMetadata(SymbolRegular.Info24));

    public EmptyStateControl()
    {
        InitializeComponent();
    }

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public string MessageText
    {
        get => (string)GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    public SymbolRegular Glyph
    {
        get => (SymbolRegular)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
}
