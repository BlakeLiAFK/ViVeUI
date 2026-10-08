using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public partial class MainWindow
{
    // Opt-in Windows-only validation. Fake feature store, isolated preferences, no updater/network/UAC.
    public async Task LocalizationSmokeAsync()
    {
        if (!demo || store is not DemoStore) throw new InvalidOperationException("Localization smoke requires the fake backend.");
        if (Root.GetBindingExpression(FlowDirectionProperty) is null) throw new Exception("Initial root direction binding is missing before any offscreen capture.");
        ThemeBox.SelectedIndex = 1; Root.Width = 1440; Root.Height = 900;
        Search = ""; searchTimer.Stop(); Category = "All"; Filter();
        foreach (var id in new uint[] { 37634385, 39420424 }) { Selected = catalog.Single(f => f.Id == id); Desired = OverrideState.Enabled; Stage(); }
        Selected = catalog.Single(f => f.Id == 37634385);
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
            if (Staged.Count != 2 || !IsEnabledOverride || Category != "All") throw new Exception("Language switching changed reviewed feature state.");
            Root.LayoutTransform = Transform.Identity; Root.Width = 1440; Root.Height = 900; UpdateLayoutMode(); IsCurated = true;
            ShowPage(ExplorePage); await Capture($"localization/{language.Code}/explore.png");
            // The fixed summary and action must remain visible even if a longer gallery needs scrolling.
            if (string.IsNullOrWhiteSpace(ObservedValue.Text) || string.IsNullOrWhiteSpace(OverrideValue.Text)) throw new Exception("Missing translated state values.");
            ShowPage(ChangesPage); await Capture($"localization/{language.Code}/review.png");
            ShowPage(SettingsPage); await Capture($"localization/{language.Code}/settings.png");
            ShowPage(UpdatesPage); await Capture($"localization/{language.Code}/updates.png");
            Root.Width = 900; UpdateLayoutMode(); ShowPage(ExplorePage); OpenDetailClick(this, new()); await Capture($"localization/{language.Code}/compact-detail.png");
            ShowPage(ChangesPage); await Capture($"localization/{language.Code}/compact-review.png");
            Root.Width = 960; Root.Height = 800; UpdateLayoutMode(); ShowPage(ExplorePage); OpenDetailClick(this, new()); await Capture($"localization/{language.Code}/scale-150.png", 1.5);
            Root.LayoutTransform = Transform.Identity; Root.Width = 1440; Root.Height = 900; UpdateLayoutMode();
            var scope = string.Join("\n", Staged.Select(c => Localization.FeatureId(c.Id) + ": " + L.State(c.Before) + " → " + L.State(c.After)));
            var dialog = LocalizedDialog.Create(this, L, L["Review"], L["UserOverride"] + "\n\n" + L["DefaultHelp"], true, @"C:\ViVeUI\downloads\ViVeUI-win-x64.exe", scope);
            var panel = (FrameworkElement)dialog.Content;
            var buttons = EnumerateLogicalChildren(panel).OfType<Button>().ToArray();
            if (buttons.Length != 2 || buttons.Single(b => b.IsDefault).Content as string != L["Cancel"] || !buttons.Single(b => b.IsDefault).IsCancel) throw new Exception("Confirmation must default to localized Cancel.");
            dialog.Content = null;
            var host = new Border { Child = panel, Width = 640, Height = 720, Background = SystemColors.WindowBrush, FlowDirection = L.Direction, Language = L.XmlLanguage };
            System.Windows.Documents.TextElement.SetFontFamily(host, L.Font);
            host.Measure(new Size(640,720)); host.Arrange(new Rect(0,0,640,720)); host.UpdateLayout();
            var bitmap = new RenderTargetBitmap(640,720,96,96,PixelFormats.Pbgra32); bitmap.Render(host);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine("previews","localization",language.Code,"confirmation.png"))) encoder.Save(stream);
            host.Child = null; dialog.Close();
            var missing = Localization.Resource(language.Code).Values.SelectMany(t => t.EnumerateRunes()).Where(r => Rune.IsLetterOrDigit(r) || Rune.GetUnicodeCategory(r) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark).Select(r => r.Value).Distinct().Where(c => !glyphs.Contains(c)).Select(c => $"U+{c:X4}").ToArray();
            missingGlyphs |= missing.Length > 0;
            reports.Add(new { language.Code, language.NativeName, language.FontFamily, rtl = language.IsRightToLeft, missingSystemGlyphs = missing, renders = 8, persisted = true, statePreserved = true, noFeatureWrites = true });
        }
        LanguageBox.SelectedValue = "system"; SaveSettings(); LoadSettings();
        if (LanguagePreference != "system" || L.Language != Localization.ResolveSelection("system")) throw new Exception("System language preference round trip failed.");
        File.WriteAllText("localization-result.json", JsonSerializer.Serialize(new { passed = !missingGlyphs, humanReviewRequired = true, glyphAvailabilityDoesNotProveShaping = true, noFeatureWrites = true, reports }, new JsonSerializerOptions { WriteIndented = true }));
        if (missingGlyphs) throw new Exception("Some translated characters have no installed Windows glyph. See localization-result.json.");
    }
    static IEnumerable<DependencyObject> EnumerateLogicalChildren(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        { yield return child; foreach (var nested in EnumerateLogicalChildren(child)) yield return nested; }
    }
}
