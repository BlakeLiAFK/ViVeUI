using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public partial class MainWindow
{
    // Opt-in Windows-only validation. Fake feature store, isolated preferences, no updater/network/UAC.
    public async Task LocalizationSmokeAsync()
    {
        if (!demo || store is not DemoStore fake) throw new InvalidOperationException("Localization smoke requires the fake backend.");
        if (Root.GetBindingExpression(FlowDirectionProperty) is null) throw new Exception("Initial root direction binding is missing before any offscreen capture.");
        ThemeBox.SelectedIndex = 1; Root.Width = 1440; Root.Height = 900;
        discovered = [new Feature(4294967201, "Demo localization one")];
        Search = ""; searchTimer.Stop(); ShowKnown = true; ShowHistorical = false; ShowUnknown = true; Filter();
        Selected = new Feature(4294967201, "Demo localization one");
        await ChangeSelectedAsync(new(true, OverrideState.Enabled));
        var expectedState = new Snapshot(true, OverrideState.Enabled);
        if (fake.Read(Selected.Id) != expectedState) throw new Exception("Immediate checkbox fixture did not apply to fake storage.");
        var glyphs = new HashSet<int>();
        foreach (var typeface in Fonts.SystemTypefaces)
            if (typeface.TryGetGlyphTypeface(out var glyph)) glyphs.UnionWith(glyph.CharacterToGlyphMap.Keys);
        var reports = new List<object>(); var missingGlyphs = false;
        foreach (var language in Localization.Languages)
        {
            SidebarLanguage.SelectedValue = language.Code;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (L.Language != language.Code || LanguageBox.SelectedValue as string != language.Code || LanguagePreference != language.Code) throw new Exception("Language selector synchronization failed: " + language.Code);
            SaveSettings();
            var saved = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(folder, "settings.json")))!;
            if (saved.Language != language.Code) throw new Exception("Language preference persistence failed.");
            var expectedDirection = language.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            if (Root.FlowDirection != expectedDirection || FlowDirection != expectedDirection) throw new Exception($"Language layout direction did not update: {language.Code}, locale={L.Direction}, root={Root.FlowDirection}, window={FlowDirection}, rootBinding={Root.GetBindingExpression(FlowDirectionProperty)?.Status}, windowBinding={GetBindingExpression(FlowDirectionProperty)?.Status}.");
            if (fake.Read(4294967201) != expectedState || ToggleState != true || !ShowKnown || ShowHistorical || !ShowUnknown) throw new Exception("Language switching changed the immediate feature state.");
            if (EnableCheckBox.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty) is null || EnableCheckBox.IsChecked != true || string.IsNullOrWhiteSpace(ToggleStateText)) throw new Exception("Immediate checkbox state binding or translated status is missing.");
            Root.LayoutTransform = Transform.Identity; Root.Width = 1440; Root.Height = 900; UpdateLayoutMode();
            ShowPage(ExplorePage); await Capture($"localization/{language.Code}/explore.png");
            // The fixed summary and action must remain visible even if a longer gallery needs scrolling.
            if (string.IsNullOrWhiteSpace(ObservedValue.Text) || string.IsNullOrWhiteSpace(OverrideValue.Text)) throw new Exception("Missing translated state values.");
            ShowPage(ChangesPage); await Capture($"localization/{language.Code}/history.png");
            ShowPage(SettingsPage); await Capture($"localization/{language.Code}/settings.png");
            ShowPage(UpdatesPage); await Capture($"localization/{language.Code}/updates.png");
            Root.Width = 900; UpdateLayoutMode(); ShowPage(ExplorePage); OpenDetailClick(this, new()); await Capture($"localization/{language.Code}/compact-detail.png");
            ShowPage(ChangesPage); await Capture($"localization/{language.Code}/compact-history.png");
            Root.Width = 960; Root.Height = 800; UpdateLayoutMode(); ShowPage(ExplorePage); OpenDetailClick(this, new()); await Capture($"localization/{language.Code}/scale-150.png", 1.5);
            Root.LayoutTransform = Transform.Identity; Root.Width = 1440; Root.Height = 900; UpdateLayoutMode();
            ShowPage(ExplorePage); SelectGuide(CuratedCatalog.All[0]); await Capture($"localization/{language.Code}/guide.png");
            Selected = new Feature(4294967201, "Demo localization one");
            var catalogStrings = CuratedCatalog.All.Select(entry => CuratedCatalog.Text(entry, language.Code)).SelectMany(text => new[] { text.Title, text.Body, text.Evidence, text.Keywords });
            var missing = Localization.Resource(language.Code).Values.Concat(catalogStrings).SelectMany(t => t.EnumerateRunes()).Where(r => Rune.IsLetterOrDigit(r) || Rune.GetUnicodeCategory(r) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark).Select(r => r.Value).Distinct().Where(c => !glyphs.Contains(c)).Select(c => $"U+{c:X4}").ToArray();
            missingGlyphs |= missing.Length > 0;
            reports.Add(new { language.Code, language.NativeName, language.FontFamily, rtl = language.IsRightToLeft, missingSystemGlyphs = missing, renders = 8, persisted = true, statePreserved = true, checkboxBindingVerified = true, noNativeSettingsModified = true });
        }
        LanguageBox.SelectedValue = "system"; SaveSettings(); LoadSettings();
        if (LanguagePreference != "system" || L.Language != Localization.ResolveSelection("system")) throw new Exception("System language preference round trip failed.");
        File.WriteAllText("localization-result.json", JsonSerializer.Serialize(new { passed = !missingGlyphs, humanReviewRequired = true, glyphAvailabilityDoesNotProveShaping = true, noNativeSettingsModified = true, reports }, new JsonSerializerOptions { WriteIndented = true }));
        if (missingGlyphs) throw new Exception("Some translated characters have no installed Windows glyph. See localization-result.json.");
    }
    static IEnumerable<DependencyObject> EnumerateLogicalChildren(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        { yield return child; foreach (var nested in EnumerateLogicalChildren(child)) yield return nested; }
    }
}
