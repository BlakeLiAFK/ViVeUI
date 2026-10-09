using System.Diagnostics;
using System.Globalization;
using ViVeUI.Core;

public static class UnifiedCatalogTests
{
    public static int Run()
    {
        int passed = 0;
        void Assert(bool condition) { if (!condition) throw new Exception("Unified catalog assertion failed."); }
        void Test(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS Unified: " + name); }
            catch (Exception error) { throw new Exception(name, error); }
        }
        var entries = CuratedCatalog.All;
        var raw = Catalog.Load();
        var represented = entries.SelectMany(entry => entry.FeatureIds).ToHashSet();
        var unknown = raw.Where(feature => !represented.Contains(feature.Id)).OrderBy(feature => feature.Id).ToArray();
        var knownCount = entries.Count(entry => entry.Kind != CuratedKind.Historical);
        var historicalCount = entries.Count - knownCount;
        Test("Defaults show only known and historical purposes with exact counts", () =>
        {
            var result = UnifiedCatalog.Query(raw, "", "en");
            Assert(result.Items.Select(item => item.Entry).SequenceEqual(entries));
            Assert(result.Items.All(item => item.Feature is null && !item.IsUnknown));
            Assert(result.KnownCount == knownCount && result.HistoricalCount == historicalCount);
            Assert(result.UnknownCount == unknown.Length && result.HiddenUnknownCount == unknown.Length && !result.HiddenUnknownMatch);
            Assert(result.Items.Count == 213 && raw.Count == 17000);
        });
        Test("All eight visibility combinations preserve independent facet counts", () =>
        {
            foreach (var showKnown in new[] { false, true })
                foreach (var showHistorical in new[] { false, true })
                    foreach (var showUnknown in new[] { false, true })
                    {
                        var result = UnifiedCatalog.Query(raw, "", "en", showKnown, showHistorical, showUnknown);
                        var expectedEntries = entries.Where(entry => entry.Kind == CuratedKind.Historical ? showHistorical : showKnown);
                        Assert(result.Items.Where(item => item.Entry is not null).Select(item => item.Entry).SequenceEqual(expectedEntries));
                        Assert(result.Items.Count == (showKnown ? knownCount : 0) + (showHistorical ? historicalCount : 0) + (showUnknown ? unknown.Length : 0));
                        Assert(result.KnownCount == knownCount && result.HistoricalCount == historicalCount && result.UnknownCount == unknown.Length);
                        Assert(result.HiddenUnknownCount == (showUnknown ? 0 : unknown.Length));
                    }
        });
        Test("Curated order precedes numeric raw order and excludes represented duplicates", () =>
        {
            var result = UnifiedCatalog.Query(raw.Reverse(), " ", "en", showUnknown: true);
            Assert(result.Items.Take(entries.Count).Select(item => item.Entry).SequenceEqual(entries));
            Assert(result.Items.Skip(entries.Count).Select(item => item.Feature).SequenceEqual(unknown));
            Assert(result.Items.Where(item => item.Feature is not null).All(item => !represented.Contains(item.Feature!.Id)));
            Assert(result.Items.All(item => (item.Entry is null) != (item.Feature is null)));
        });
        Test("Raw dictionary and discoveries deduplicate by ID without inventing known purpose", () =>
        {
            var existing = unknown[0];
            var discovered = new Feature(4294967201, "Demo discovered technical name");
            var input = raw.Concat([existing with { Name = "Duplicate alias" }, discovered, discovered]);
            var result = UnifiedCatalog.Query(input, "", "en", showUnknown: true);
            Assert(result.UnknownCount == unknown.Length + 1);
            Assert(result.Items.Count(item => item.Feature?.Id == existing.Id) == 1);
            Assert(result.Items.Single(item => item.Feature?.Id == existing.Id).Feature == existing);
            Assert(result.Items.Single(item => item.Feature?.Id == discovered.Id).IsUnknown);
            Assert(result.KnownCount == knownCount && result.HistoricalCount == historicalCount);
        });
        Test("Exact hidden unknown ID does not bypass visibility", () =>
        {
            var feature = new Feature(4294967201, "Demo technical only");
            var query = "  " + feature.Id.ToString(CultureInfo.InvariantCulture) + "  ";
            var hidden = UnifiedCatalog.Query([feature], query, "en");
            Assert(hidden.Items.Count == 0 && hidden.UnknownCount == 1 && hidden.HiddenUnknownCount == 1 && hidden.HiddenUnknownMatch);
            var shown = UnifiedCatalog.Query([feature], query, "en", showUnknown: true);
            Assert(shown.Items.Single().Feature == feature && shown.UnknownCount == 1 && !shown.HiddenUnknownMatch);
        });
        Test("Unprovided arbitrary numeric input never synthesizes a feature", () =>
        {
            var result = UnifiedCatalog.Query(raw, "4294967295", "en", showUnknown: true);
            Assert(result.Items.Count == 0 && result.UnknownCount == 0 && !result.HiddenUnknownMatch);
        });
        Test("Mapped raw technical names find curated references without raw duplicates", () =>
        {
            var reference = entries.First(entry => entry.FeatureIds.Count > 0).FeatureIds[0];
            const string alias = "DemoMappedTechnicalAliasToken";
            var matches = entries.Where(entry => entry.FeatureIds.Contains(reference)).ToArray();
            var result = UnifiedCatalog.Query([new(reference, alias)], "  " + alias.ToLowerInvariant() + "  ", "en", showUnknown: true);
            Assert(result.Items.Select(item => item.Entry).SequenceEqual(matches));
            Assert(result.UnknownCount == 0 && result.Items.All(item => item.Feature is null));
        });
        Test("Referenced numeric IDs obey historical visibility", () =>
        {
            var historical = entries.First(entry => entry.Kind == CuratedKind.Historical && entry.FeatureIds.Count > 0);
            var query = historical.FeatureIds[0].ToString(CultureInfo.InvariantCulture);
            var hidden = UnifiedCatalog.Query(raw, query, "en", showKnown: false, showHistorical: false, showUnknown: true);
            Assert(hidden.HistoricalCount > 0 && hidden.Items.All(item => !item.IsHistorical));
            Assert(hidden.Items.All(item => item.Feature?.Id != historical.FeatureIds[0]));
            var shown = UnifiedCatalog.Query(raw, query, "en", showKnown: false, showHistorical: true);
            Assert(shown.Items.Any(item => item.Entry == historical));
        });
        Test("All sixteen localized titles and keywords find their curated entries", () =>
        {
            foreach (var language in Localization.Languages)
                foreach (var entry in entries)
                {
                    var text = CuratedCatalog.Text(entry, language.Code);
                    Assert(UnifiedCatalog.Query([], " " + text.Title + " ", language.Code).Items.Any(item => item.Entry == entry));
                    Assert(UnifiedCatalog.Query([], " " + text.Keywords + " ", language.Code).Items.Any(item => item.Entry == entry));
                }
        });
        Test("Empty results and unknown technical-name search keep accurate facets", () =>
        {
            var empty = UnifiedCatalog.Query(raw, "no-matching-catalog-purpose-4294967201999", "en", showUnknown: true);
            Assert(empty.Items.Count == 0 && empty.KnownCount == 0 && empty.HistoricalCount == 0 && empty.UnknownCount == 0);
            var result = UnifiedCatalog.Query([new(4294967201, "DemoTechnicalOnlyPurposeUnknown")], "demotechnicalonly", "en");
            Assert(result.Items.Count == 0 && result.KnownCount == 0 && result.HistoricalCount == 0 && result.UnknownCount == 1);
        });
        Test("Repeated 17000-ID queries remain bounded within ten seconds", () =>
        {
            _ = UnifiedCatalog.Query(raw, "", "en", showUnknown: true);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 250; i++)
            {
                var result = UnifiedCatalog.Query(raw, i % 2 == 0 ? "explorer" : "37634385", "en", showUnknown: i % 3 == 0);
                Assert(result.Items.Count <= entries.Count + raw.Count);
            }
            Assert(timer.Elapsed < TimeSpan.FromSeconds(10));
        });
        return passed;
    }
}
