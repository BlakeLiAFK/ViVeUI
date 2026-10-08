using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ViVeUI.Core;

namespace ViVeUI.Windows;

public partial class MainWindow
{
    // Opt-in native WPF validation against embedded production data; never launches a destination.
    public async Task CatalogSmokeAsync()
    {
        if (!demo || store is not DemoStore) throw new InvalidOperationException("Catalog smoke requires the fake backend.");
        var checks = new List<string>();
        var captures = new List<string>();
        var pages = new List<string[]>();
        var languageResults = new List<object>();
        var entries = CuratedCatalog.All;
        var originalQueue = Staged.ToArray();
        string? failure = null;
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Query(string query) { Search = query; searchTimer.Stop(); Filter(); }
        string[] Matches() => catalogMatches.Select(e => e.Id).ToArray();
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        async Task Shot(string name) { await Capture("catalog/" + name); captures.Add("previews/catalog/" + name); }
        void AssertDetail(CuratedEntry entry)
        {
            var text = CuratedCatalog.Text(entry, L.Language);
            Require(selectedGuide?.Id == entry.Id && GuideBody == text.Body && GuideEvidence == text.Evidence && GuideTitle == text.Title,
                "Localized catalog details mismatch: " + L.Language + "/" + entry.Id);
            var renderedBody = Descendants(GuideDetailPane).OfType<TextBlock>().SingleOrDefault(t => t.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == nameof(GuideBody));
            Require(renderedBody is not null && renderedBody.Text == text.Body, "WPF detail body binding did not update.");
        }
        try
        {
            Require(entries.Count >= 200, "Embedded catalog has fewer than 200 entries.");
            CuratedCatalog.Validate(entries);
            Root.LayoutTransform = Transform.Identity; Root.Width = 1440; Root.Height = 900;
            ThemeBox.SelectedIndex = 1; LanguageBox.SelectedValue = "en"; IsCurated = true;
            CuratedCategory = "All"; CuratedType = "All"; Query(""); UpdateLayoutMode(); ShowPage(ExplorePage);
            SelectGuide(entries[0]);
            Require(CatalogPageSize <= 12, "Catalog page exceeds the twelve-card budget.");
            Require(catalogPage == 0 && !CanPreviousCatalogPage, "Initial page is not the first page.");
            while (true)
            {
                await Idle();
                var cards = Cards.ToArray();
                Require(cards.Length is > 0 and <= 12 && cards.All(c => c.Guide is not null), "Catalog page contains invalid cards.");
                var ids = cards.Select(c => c.Guide!.Id).ToArray();
                Require(ids.SequenceEqual(entries.Skip(catalogPage * CatalogPageSize).Take(CatalogPageSize).Select(e => e.Id)), "Page order differs from the stable catalog order.");
                pages.Add(ids);
                if (catalogPage == 0) await Shot("full-first-page.png");
                if (!CanNextCatalogPage) break;
                NextCatalogPageClick(this, new());
                Require(pages.Count <= CatalogPageCount, "Pagination did not advance.");
            }
            Require(pages.Count == CatalogPageCount && pages.SelectMany(p => p).SequenceEqual(entries.Select(e => e.Id)) && pages.SelectMany(p => p).Distinct().Count() == entries.Count,
                "Pagination lost or duplicated a stable catalog ID.");
            await Shot("full-last-page.png");
            while (CanPreviousCatalogPage) PreviousCatalogPageClick(this, new());
            Require(catalogPage == 0, "Previous-page navigation failed.");
            checks.Add("Every stable ID visited once in order; pages contain at most twelve cards; next/previous navigation verified.");

            foreach (var category in CuratedCategories.Select(c => c.Key).ToArray())
                foreach (var kind in CuratedTypes.Select(c => c.Key).ToArray())
                {
                    CuratedCategory = category; CuratedType = kind;
                    var expected = entries.Where(e => (category == "All" || e.Category == category) && (kind == "All" || e.Kind.ToString() == kind)).Select(e => e.Id);
                    Require(Matches().SequenceEqual(expected), "Filter count/order mismatch: " + category + "/" + kind);
                    Require(Cards.Count() == Math.Min(CatalogPageSize, catalogMatches.Count), "Filtered card count mismatch.");
                    Require(catalogPage == 0, "Filter change did not reset pagination.");
                    foreach (var option in CuratedCategories)
                    {
                        var count = entries.Count(e => (kind == "All" || e.Kind.ToString() == kind) && (option.Key == "All" || e.Category == option.Key));
                        Require(option.Value.EndsWith("(" + count.ToString(CultureInfo.CurrentCulture) + ")", StringComparison.Ordinal), "Category badge count mismatch.");
                    }
                    foreach (var option in CuratedTypes)
                    {
                        var count = entries.Count(e => (category == "All" || e.Category == category) && (option.Key == "All" || e.Kind.ToString() == option.Key));
                        Require(option.Value.EndsWith("(" + count.ToString(CultureInfo.CurrentCulture) + ")", StringComparison.Ordinal), "Type badge count mismatch.");
                    }
                }
            checks.Add("All category/type intersections and facet badge counts match the catalog.");
            CuratedCategory = "All"; CuratedType = "All";
            var historical = entries.Where(e => e.Kind == CuratedKind.Historical).ToArray();
            Require(historical.Length > 0, "No historical entry available for read-only validation.");
            Selected = catalog.First(); Desired = OverrideState.Enabled;
            foreach (var entry in historical)
            {
                SelectGuide(entry);
                Require(selectedGuide is not null && !CanStage && entry.Destination is null, "Historical entry permits feature staging or direct launch.");
                Stage();
                Require(Staged.SequenceEqual(originalQueue), "Historical card changed the review queue.");
            }
            checks.Add("All historical cards remain read-only even with a raw feature selected.");
            var reference = historical.FirstOrDefault(e => e.FeatureIds.Count > 0) ?? entries.FirstOrDefault(e => e.FeatureIds.Count > 0);
            Require(reference is not null, "No feature reference exists to validate numeric search and LTR IDs.");
            foreach (var id in reference!.FeatureIds)
            {
                Query(id.ToString(CultureInfo.InvariantCulture));
                Require(catalogMatches.Any(e => e.Id == reference.Id), "Numeric reference-ID search lost its catalog entry.");
            }
            checks.Add("Numeric feature-reference search finds the documented entry.");

            foreach (var language in Localization.Languages)
            {
                LanguageBox.SelectedValue = language.Code; Query(""); SelectGuide(reference); await Idle();
                Require(L.Language == language.Code, "Language selector did not update.");
                AssertDetail(reference);
                var text = CuratedCatalog.Text(reference, language.Code);
                Query(text.Title); var trimmed = Matches(); Query("  " + text.Title + "  ");
                Require(Matches().SequenceEqual(trimmed) && trimmed.Contains(reference.Id), "Localized whitespace search differs from trimmed search.");
                Query("");
                var direction = language.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                Require(Root.FlowDirection == direction && FlowDirection == direction, "Catalog layout direction mismatch: " + language.Code);
                var referenceText = Descendants(GuideDetailPane).OfType<TextBlock>().Single(t => t.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == nameof(GuideReferenceIds));
                Require(referenceText.FlowDirection == FlowDirection.LeftToRight && referenceText.Text == string.Join(" · ", reference.FeatureIds.Select(id => id.ToString(CultureInfo.InvariantCulture))), "Reference IDs lost LTR invariant formatting.");
                await Shot("detail-" + language.Code + ".png");
                languageResults.Add(new { language = language.Code, detailId = reference.Id, bodyMatches = true, evidenceMatches = true, trimmedSearchMatches = true, rtl = language.IsRightToLeft, referenceIdsLeftToRight = true });
            }
            checks.Add("Sixteen languages display catalog detail text; localized search trims whitespace; Arabic layout is RTL and numeric references remain LTR.");
            LanguageBox.SelectedValue = "en";
            Query("catalog-smoke-no-match-90df1685");
            Require(!Cards.Any() && GalleryEmptyVisibility == Visibility.Visible && CatalogPageCount == 1 && !CanNextCatalogPage && !CanPreviousCatalogPage, "Empty search state/pagination is incorrect.");
            await Shot("empty-search.png");
            ClearSearchClick(this, new());
            Require(catalogMatches.Count == entries.Count && Cards.Count() == CatalogPageSize && GalleryEmptyVisibility == Visibility.Collapsed, "Clear search did not restore the catalog.");
            checks.Add("Empty state and clear-search reset verified.");

            Root.Width = 900; UpdateLayoutMode();
            Require(compact && CatalogPane.Visibility == Visibility.Visible && GuideDetailPane.Visibility == Visibility.Collapsed, "Compact catalog layout is incorrect.");
            await Shot("compact-cards.png");
            var card = Cards.First(); CardClick(new Button { Tag = card }, new()); await Idle();
            Require(detailOpen && CatalogPane.Visibility == Visibility.Collapsed && GuideDetailPane.Visibility == Visibility.Visible && DetailPane.Visibility == Visibility.Collapsed, "Compact card did not open its guide detail.");
            AssertDetail(card.Guide!); await Shot("compact-detail.png");
            Root.Width = 1440; UpdateLayoutMode(); await Idle();
            Require(!compact && CatalogPane.Visibility == Visibility.Visible && GuideDetailPane.Visibility == Visibility.Visible, "Full catalog/detail panes were not restored.");
            AssertDetail(card.Guide!); await Shot("full-detail.png");
            Require(Staged.SequenceEqual(originalQueue), "Catalog smoke modified the review queue.");
            checks.Add("Compact cards/detail and full split view verified; review queue preserved.");
        }
        catch (Exception ex) { failure = ex.Message; throw; }
        finally
        {
            searchTimer.Stop();
            File.WriteAllText("catalog-result.json", JsonSerializer.Serialize(new
            {
                passed = failure is null, failure, entryCount = entries.Count,
                pageSize = CatalogPageSize, pagesVisited = pages.Count, visitedIds = pages.SelectMany(p => p).ToArray(),
                byKind = entries.GroupBy(e => e.Kind.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                byCategory = entries.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count()),
                checks, languages = languageResults, captures, noNativeSettingsModified = true,
                humanVisualReviewRequired = true
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
