using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ViVeUI.Core;

public static class CuratedTests
{
    public static int Run()
    {
        int passed = 0;
        void Assert(bool condition) { if (!condition) throw new Exception("Catalog assertion failed."); }
        void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS Curated: " + name); } catch (Exception ex) { throw new Exception(name, ex); } }
        void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } catch (JsonException) { return; } throw new Exception("Malformed catalog was accepted."); }
        var entries = CuratedCatalog.All;
        string Resource(string name)
        {
            using var stream = typeof(CuratedCatalog).Assembly.GetManifestResourceStream("ViVeUI.Core.Catalog." + name)!;
            using var reader = new StreamReader(stream); return reader.ReadToEnd();
        }
        var locales = Localization.Languages.ToDictionary(l => l.Code, l => CuratedCatalog.ParseLocale(Resource("Locales." + l.Code + ".json")));
        var sample = entries.First();
        Test("At least 200 distinct sourced tasks", () =>
        {
            Assert(entries.Count >= 200 && entries.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == entries.Count);
            CuratedCatalog.Validate(entries);
            Assert(entries.All(e => e.Sources.Count > 0 && e.Sources.All(CuratedCatalog.IsAllowedSource) && !string.IsNullOrWhiteSpace(e.Body) && !string.IsNullOrWhiteSpace(e.Evidence)));
        });
        Test("Sixteen complete localized catalogs", () =>
        {
            Assert(locales.Count == 16);
            CuratedCatalog.ValidateLocales(entries, locales);
            foreach (var entry in entries)
                Assert(locales["en"][entry.Id] == new CuratedText(entry.Title, entry.Body, entry.Keywords, entry.Evidence));
        });
        Test("Historical entries cannot launch or stage feature overrides", () =>
        {
            Assert(entries.All(e => e.Kind != CuratedKind.FeatureFlag));
            Assert(entries.Where(e => e.Kind == CuratedKind.Historical).All(e => e.Destination is null));
            Reject(() => CuratedCatalog.Validate([sample with { Kind = CuratedKind.Historical, Destination = "explorer.exe" }]));
            Reject(() => CuratedCatalog.Validate([sample with { Kind = CuratedKind.FeatureFlag, FeatureIds = new uint[] { 37634385 } }]));
        });
        Test("Destination injection is rejected", () =>
        {
            foreach (var value in new[] { "cmd.exe", "explorer.exe /select,C:\\secret", "ms-settings:display?x=1", "ms-settings:unknown", "https://example.com", "ms-settings:display\n", " ms-settings:display", "ms-settings:display;calc.exe" })
            {
                Assert(!CuratedCatalog.IsAllowedDestination(value));
                Reject(() => CuratedCatalog.Validate([sample with { Destination = value }]));
            }
        });
        Test("Missing, malformed, duplicate and credentialed sources are rejected", () =>
        {
            Reject(() => CuratedCatalog.Validate([sample with { Sources = Array.Empty<string>() }]));
            foreach (var value in new[] { "", "   ", "documentation", "http://support.microsoft.com/article", "javascript:alert(1)", "https://user:password@support.microsoft.com/article", "https://support.microsoft.com:444/article" })
                Reject(() => CuratedCatalog.Validate([sample with { Sources = new[] { value } }]));
            Reject(() => CuratedCatalog.Validate([sample with { Sources = new[] { sample.Sources[0], sample.Sources[0] } }]));
        });
        Test("Duplicate IDs and normalized duplicate tasks are rejected", () =>
        {
            Reject(() => CuratedCatalog.Validate([sample, sample]));
            Reject(() => CuratedCatalog.Validate([sample, sample with { Id = "DuplicateProbe", Title = "  " + sample.Title.ToUpperInvariant() + "!" }]));
            Reject(() => CuratedCatalog.Validate([sample, sample with { Id = "DuplicateProbe", Title = "Distinct test title", Body = "  " + sample.Body.ToUpperInvariant() + "!" }]));
        });
        Test("Feature references allow later IDs and shared dependencies", () =>
        {
            var historical = sample with { Kind = CuratedKind.Historical, Destination = null, FeatureIds = new uint[] { uint.MaxValue } };
            CuratedCatalog.Validate([historical, historical with { Id = "DependencyProbe", Title = "Separate documented operation", Body = "Review a different historical behavior before making any changes." }]);
            Reject(() => CuratedCatalog.Validate([historical with { FeatureIds = new uint[] { 0 } }]));
            Reject(() => CuratedCatalog.Validate([historical with { FeatureIds = new uint[] { 42, 42 } }]));
        });
        Test("Malformed structure and duplicate locale keys are rejected", () =>
        {
            Reject(() => CuratedCatalog.Parse("{}"));
            Reject(() => CuratedCatalog.Parse("[{}]"));
            Reject(() => CuratedCatalog.ParseLocale("[]"));
            var text = "{\"title\":\"T\",\"body\":\"B\",\"keywords\":\"K\",\"evidence\":\"E\"}";
            Reject(() => CuratedCatalog.ParseLocale("{\"Same\":" + text + ",\"Same\":" + text + "}"));
            Reject(() => CuratedCatalog.ParseLocale("{\"One\":{\"title\":\"T\"}}"));
        });
        Test("Missing, extra, empty and English-cloned locale content is rejected", () =>
        {
            void RejectText(Func<Dictionary<string, CuratedText>, Dictionary<string, CuratedText>> mutate)
            {
                var altered = new Dictionary<string, IReadOnlyDictionary<string, CuratedText>>(locales);
                altered["fr"] = mutate(new Dictionary<string, CuratedText>(locales["fr"]));
                Reject(() => CuratedCatalog.ValidateLocales(entries, altered));
            }
            RejectText(d => { d.Remove(sample.Id); return d; });
            RejectText(d => { d["UnexpectedProbe"] = d[sample.Id]; return d; });
            RejectText(d => { d[sample.Id] = d[sample.Id] with { Keywords = " " }; return d; });
            RejectText(d => { d[sample.Id] = d[sample.Id] with { Evidence = " " }; return d; });
            RejectText(d => { d[sample.Id] = d[sample.Id] with { Body = sample.Body }; return d; });
            var missingLanguage = new Dictionary<string, IReadOnlyDictionary<string, CuratedText>>(locales); missingLanguage.Remove("fr");
            Reject(() => CuratedCatalog.ValidateLocales(entries, missingLanguage));
        });
        Test("Localized title and keyword search trims whitespace", () =>
        {
            foreach (var language in Localization.Languages)
                foreach (var entry in entries)
                {
                    var text = CuratedCatalog.Text(entry, language.Code);
                    Assert(CuratedCatalog.Search("  " + text.Title + "  ", language.Code).Any(e => e.Id == entry.Id));
                    Assert(CuratedCatalog.Search(text.Keywords, language.Code).Any(e => e.Id == entry.Id));
                }
        });
        Test("Feature ID reference search is invariant", () =>
        {
            foreach (var entry in entries)
                foreach (var id in entry.FeatureIds)
                    Assert(CuratedCatalog.Search(id.ToString(CultureInfo.InvariantCulture), "ar").Any(e => e.Id == entry.Id));
        });
        Test("Category and kind filters preserve exact deterministic counts", () =>
        {
            Assert(CuratedCatalog.Search(" ", "en").Select(e => e.Id).SequenceEqual(entries.Select(e => e.Id)));
            foreach (var category in entries.Select(e => e.Category).Distinct().Append("UnknownCategory"))
                foreach (var kind in Enum.GetNames<CuratedKind>().Append("All"))
                {
                    var expected = entries.Where(e => e.Category == category && (kind == "All" || e.Kind.ToString() == kind)).Select(e => e.Id);
                    Assert(CuratedCatalog.Search("", "en", category, kind).Select(e => e.Id).SequenceEqual(expected));
                }
        });
        Test("Ten thousand searches complete within a generous ten-second budget", () =>
        {
            _ = CuratedCatalog.Search("explorer", "en").Count();
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 10_000; i++) _ = CuratedCatalog.Search(i % 2 == 0 ? "explorer" : "nonexistent-probe", "en").Count();
            Assert(timer.Elapsed < TimeSpan.FromSeconds(10));
        });
        return passed;
    }
}
