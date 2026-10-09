using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public partial class MainWindow
{
    public const int CatalogPageSize = 12;
    bool showKnown = true, showHistorical = true, showUnknown;
    string lastCatalogCriteria = "";
    int catalogPage, catalogTotal, knownMatchCount, historicalMatchCount, unknownMatchCount, hiddenUnknownMatchCount;
    bool hiddenUnknownMatch;
    IReadOnlyList<UnifiedCatalogItem> catalogMatches = [];

    public bool ShowKnown { get => showKnown; set { if(showKnown == value)return; showKnown = value; Changed(); Filter(); } }
    public bool ShowHistorical { get => showHistorical; set { if(showHistorical == value)return; showHistorical = value; Changed(); Filter(); } }
    public bool ShowUnknown { get => showUnknown; set { if(showUnknown == value)return; showUnknown = value; Changed(); Filter(); } }
    public int KnownMatchCount => knownMatchCount;
    public int HistoricalMatchCount => historicalMatchCount;
    public int UnknownMatchCount => unknownMatchCount;
    public int HiddenUnknownMatchCount => hiddenUnknownMatchCount;
    public string FilterKnownText => L.Format("FilterKnown", knownMatchCount);
    public string FilterHistoricalText => L.Format("FilterHistorical", historicalMatchCount);
    public string FilterUnknownText => L.Format("FilterUnknown", unknownMatchCount);
    public Visibility HiddenUnknownVisibility => hiddenUnknownMatch ? Visibility.Visible : Visibility.Collapsed;
    public string HiddenUnknownText => L["FilterHiddenMatch"];
    public string CuratedCountText => L.Format("CatalogCountFormat", catalogMatches.Count, catalogTotal);
    public int CatalogPageCount => Math.Max(1, (catalogMatches.Count + CatalogPageSize - 1) / CatalogPageSize);
    public string CatalogPageText => L.Format("CatalogPageFormat", catalogPage + 1, CatalogPageCount);
    public bool CanPreviousCatalogPage => catalogPage > 0;
    public bool CanNextCatalogPage => catalogPage + 1 < CatalogPageCount;
    public Visibility GalleryEmptyVisibility => catalogMatches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public bool IsFeatureVisible(uint id) => catalogMatches.Any(item => item.Feature?.Id == id);
    public bool IsGuideVisible(string id) => catalogMatches.Any(item => item.Entry?.Id == id);

    void InitializeCuratedCatalog()
    {
        RefreshCards();
    }
    void RefreshCards()
    {
        var criteria = string.Join("\u001f", Search.Trim(), L.Language, showKnown, showHistorical, showUnknown);
        if(criteria != lastCatalogCriteria)
        {
            catalogPage = 0;
            lastCatalogCriteria = criteria;
            GalleryScroll?.ScrollToTop();
        }
        var raw = catalog.Concat(discovered).ToArray();
        var result = UnifiedCatalog.Query(raw, Search, L.Language, showKnown, showHistorical, showUnknown);
        catalogMatches = result.Items;
        knownMatchCount = result.KnownCount;
        historicalMatchCount = result.HistoricalCount;
        unknownMatchCount = result.UnknownCount;
        hiddenUnknownMatchCount = result.HiddenUnknownCount;
        hiddenUnknownMatch = result.HiddenUnknownMatch;
        var referenced = CuratedCatalog.All.SelectMany(entry => entry.FeatureIds).ToHashSet();
        catalogTotal = CuratedCatalog.All.Count + raw.Select(feature => feature.Id).Distinct().Count(id => id != 0 && !referenced.Contains(id));
        catalogPage = Math.Min(catalogPage, CatalogPageCount - 1);
        foreach(var name in new[]
        {
            nameof(Cards), nameof(CuratedCountText), nameof(GalleryEmptyVisibility),
            nameof(CatalogPageText), nameof(CanPreviousCatalogPage), nameof(CanNextCatalogPage),
            nameof(FilterKnownText), nameof(FilterHistoricalText), nameof(FilterUnknownText),
            nameof(KnownMatchCount), nameof(HistoricalMatchCount), nameof(UnknownMatchCount), nameof(HiddenUnknownMatchCount),
            nameof(HiddenUnknownVisibility), nameof(HiddenUnknownText)
        })Changed(name);
    }
    IEnumerable<FeatureCard> CuratedCards()
    {
        foreach(var item in catalogMatches.Skip(catalogPage * CatalogPageSize).Take(CatalogPageSize))
        {
            if(item.Entry is CuratedEntry entry)
            {
                var text = CuratedCatalog.Text(entry, L.Language);
                yield return new(null, text.Title, text.Body, L["CatalogCategory" + entry.Category],
                    (ImageSource)FindResource(entry.Illustration + "Illustration"), L["CatalogKind" + entry.Kind],
                    L["LearnMore"] + "  ›", selectedGuide?.Id == entry.Id, L["CatalogKind" + entry.Kind], entry);
            }
            else if(item.Feature is Feature feature)
            {
                // A dictionary name is an identifier, not evidence of a visual feature or its purpose.
                yield return new(feature, feature.Name, L["UnknownDescription"],
                    "ID " + feature.Id.ToString(CultureInfo.InvariantCulture), null, L["FilterUnknownKind"],
                    L["LearnMore"] + "  ›", selectedGuide is null && Selected?.Id == feature.Id, L["FilterUnknownKind"]);
            }
        }
    }
    void PreviousCatalogPageClick(object sender, RoutedEventArgs e)
    {
        if(!CanPreviousCatalogPage)return;
        catalogPage--; RefreshCards(); GalleryScroll.ScrollToTop();
    }
    void NextCatalogPageClick(object sender, RoutedEventArgs e)
    {
        if(!CanNextCatalogPage)return;
        catalogPage++; RefreshCards(); GalleryScroll.ScrollToTop();
    }
}
