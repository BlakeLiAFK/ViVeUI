using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public sealed class Locale : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Language { get; private set; } = Localization.ResolveSelection("system");
    public string this[string key] => Localization.Get(Language, key);
    public FlowDirection Direction => Localization.Definition(Language).IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public FontFamily Font => new(Localization.Definition(Language).FontFamily);
    public XmlLanguage XmlLanguage => System.Windows.Markup.XmlLanguage.GetLanguage(Localization.Definition(Language).CultureName);
    public void Set(string selection)
    {
        Language = Localization.ResolveSelection(selection);
        var culture = CultureInfo.GetCultureInfo(Localization.Definition(Language).CultureName);
        CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new("Item[]"));
        PropertyChanged?.Invoke(this, new(nameof(Direction))); PropertyChanged?.Invoke(this, new(nameof(Font))); PropertyChanged?.Invoke(this, new(nameof(XmlLanguage)));
    }
    public string Format(string key, params object[] values) => Localization.Format(Language, key, values);
    public string State(Snapshot value) => this[value.Exists && value.State == OverrideState.Default ? "PresentDefault" : value.Exists ? value.State.ToString() : "Default"];
    public string ErrorSummary(Exception error) => Localization.Resource(Language).Values.Contains(error.Message) ? error.Message : this[Localization.ErrorKey(error)];
}
public sealed class LanguageChoice(string code, string nativeName, Locale locale) : INotifyPropertyChanged
{
    public string Code => code;
    public string NativeName => code == "system" ? locale["SystemLanguage"] : nativeName;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new(nameof(NativeName)));
}
