using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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
        var originalStates = catalog.Concat(discovered).Select(feature => feature.Id).Concat(entries.SelectMany(entry => entry.FeatureIds)).Append(4294967200u).Distinct().ToDictionary(id => id, id => store.Read(id));
        bool StorageUnchanged() => originalStates.All(pair => store.Read(pair.Key) == pair.Value);
        string? failure = null;
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Query(string query) { Search = query; searchTimer.Stop(); Filter(); }
        string[] Matches() => catalogMatches.Select(e => e.Id).ToArray();
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        async Task Shot(string name) { await Capture("catalog/" + name); captures.Add("previews/catalog/" + name); }
        string CheckboxText(CheckBox checkbox) => checkbox.Content is TextBlock text ? text.Text : checkbox.Content as string ?? "";
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
            ThemeBox.SelectedIndex = 1; LanguageBox.SelectedValue = "en";
            ShowKnown = true; ShowHistorical = true; ShowUnknown = false; Query(""); UpdateLayoutMode(); ShowPage(ExplorePage);
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

            async Task Filters(bool known, bool history, bool unknown)
            {
                foreach (var (checkbox, expected) in new[] { (ShowKnownFilter, known), (ShowHistoricalFilter, history), (ShowUnknownFilter, unknown) })
                {
                    if (checkbox.IsChecked != expected)
                        ((IToggleProvider)new CheckBoxAutomationPeer(checkbox).GetPattern(PatternInterface.Toggle)!).Toggle();
                    await Idle();
                    Require(checkbox.IsChecked == expected, "Filter checkbox did not retain its requested state.");
                }
                Require(ShowKnown == known && ShowHistorical == history && ShowUnknown == unknown, "Filter checkbox bindings did not update visibility state.");
            }
            foreach (var known in new[] { false, true })
                foreach (var history in new[] { false, true })
                    foreach (var unknown in new[] { false, true })
                    {
                        await Filters(known, history, unknown);
                        var expected = UnifiedCatalog.Query(catalog.Concat(discovered), "", L.Language, known, history, unknown);
                        Require(Matches().SequenceEqual(expected.Items.Select(item => item.Id)), "Visibility-filter results differ from the unified catalog.");
                        Require(KnownMatchCount == expected.KnownCount && HistoricalMatchCount == expected.HistoricalCount && UnknownMatchCount == expected.UnknownCount, "Visibility-filter counts differ from search matches.");
                        Require(CheckboxText(ShowKnownFilter) == L.Format("FilterKnown", expected.KnownCount) && CheckboxText(ShowHistoricalFilter) == L.Format("FilterHistorical", expected.HistoricalCount) && CheckboxText(ShowUnknownFilter) == L.Format("FilterUnknown", expected.UnknownCount), "Visible checkbox count labels do not match the query.");
                        Require(Cards.Count() == Math.Min(CatalogPageSize, expected.Items.Count), "Filtered card count mismatch.");
                        Require(catalogPage == 0, "Changing a visibility filter did not reset pagination.");
                        Require(StorageUnchanged(), "Changing a visibility filter modified feature storage.");
                    }
            await Filters(false, false, true);
            Require(Cards.All(card => card.Guide is null && card.Feature is not null && card.Image is null), "Unknown-purpose cards acquired curated descriptions or illustrations.");
            if (CanNextCatalogPage)
            {
                NextCatalogPageClick(this, new());
                Require(catalogPage == 1, "Unknown results could not advance a page.");
                await Filters(true, false, true);
                Require(catalogPage == 0, "Checkbox visibility change did not return to the first page.");
            }
            var referencedIds = entries.SelectMany(entry => entry.FeatureIds).ToHashSet();
            var rawProbe = catalog.First(feature => !referencedIds.Contains(feature.Id) && !CuratedCatalog.Search(feature.Id.ToString(CultureInfo.InvariantCulture), "en").Any());
            await Filters(true, true, false); Query(rawProbe.Id.ToString(CultureInfo.InvariantCulture));
            Require(!catalogMatches.Any(item => item.Feature?.Id == rawProbe.Id) && HiddenUnknownVisibility == Visibility.Visible && HiddenUnknownMatchCount > 0, "An exact hidden numeric ID bypassed the unknown filter or lost its explanation.");
            await Shot("hidden-numeric-id.png");
            await Filters(true, true, true);
            Require(ShowUnknown && catalogMatches.Any(item => item.Feature?.Id == rawProbe.Id) && HiddenUnknownVisibility == Visibility.Collapsed, "Explicitly showing unknown results did not reveal the searched ID.");
            Require(Cards.Where(card => card.Feature is not null).All(card => card.Image is null), "Unknown-purpose result gained a fabricated illustration.");
            await Shot("unknown-numeric-id.png");
            Require(StorageUnchanged(), "Showing a hidden raw ID modified feature storage.");
            await Filters(true, true, false); Query("");
            checks.Add("All eight checkbox combinations, matching counts, pagination reset and explicit hidden numeric-ID reveal verified without feature writes.");
            var historical = entries.Where(e => e.Kind == CuratedKind.Historical).ToArray();
            Require(historical.Length > 0, "No historical entry available for read-only validation.");
            Selected = catalog.First();
            foreach (var entry in historical)
            {
                SelectGuide(entry);
                Require(selectedGuide is not null && !CanToggle && !CanRestoreDefault && entry.Destination is null, "Historical entry permits immediate feature changes or direct launch.");
                await Idle();
                Require(!EnableCheckBox.IsEnabled, "Historical entry left the checkbox enabled.");
                await ChangeSelectedAsync(new Snapshot(true, OverrideState.Enabled));
                Require(StorageUnchanged(), "Historical card changed feature storage.");
            }
            checks.Add("All historical cards disable the checkbox and default action even with a raw feature selected.");
            var reference = historical.FirstOrDefault(e => e.FeatureIds.Count > 0) ?? entries.FirstOrDefault(e => e.FeatureIds.Count > 0);
            Require(reference is not null, "No feature reference exists to validate numeric search and LTR IDs.");
            foreach (var id in reference!.FeatureIds)
            {
                Query(id.ToString(CultureInfo.InvariantCulture));
                Require(catalogMatches.Any(e => e.Id == reference.Id), "Numeric reference-ID search lost its catalog entry.");
            }
            checks.Add("Numeric feature-reference search finds the documented entry without enabling unknown results.");

            await Filters(false, true, false);
            foreach (var language in Localization.Languages)
            {
                LanguageBox.SelectedValue = language.Code; Query(""); SelectGuide(reference); await Idle();
                Require(L.Language == language.Code, "Language selector did not update.");
                Require(!ShowKnown && ShowHistorical && !ShowUnknown && ShowKnownFilter.IsChecked == false && ShowHistoricalFilter.IsChecked == true && ShowUnknownFilter.IsChecked == false, "Language switching changed visibility filters.");
                Require(StorageUnchanged(), "Language switching modified feature storage.");
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
            LanguageBox.SelectedValue = "en"; await Filters(true, true, false);
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
            Require(StorageUnchanged(), "Catalog smoke modified feature storage.");
            checks.Add("Compact cards/detail and full split view verified; feature storage preserved.");
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
