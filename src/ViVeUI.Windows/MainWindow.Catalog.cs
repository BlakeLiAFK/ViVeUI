using System.Windows;
using System.Windows.Media;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public partial class MainWindow
{
    public const int CatalogPageSize = 12;
    static readonly string[] CatalogCategoryKeys = ["ContextMenu","Explorer","Taskbar","Start","Windows","Input","Accessibility","Appearance","Notifications","Performance","SystemTools","Privacy"];
    static readonly string[] CatalogKindKeys = ["NativeShortcut","NativeSettings","Guide","Historical","FeatureFlag"];
    string curatedType = "All", curatedCategory = "All", lastCatalogCriteria = "";
    int catalogPage;
    IReadOnlyList<CuratedEntry> catalogMatches = [];
    public IReadOnlyList<CatalogFilterChoice> CuratedTypes { get; private set; } = [];
    public IReadOnlyList<CatalogFilterChoice> CuratedCategories { get; private set; } = [];
    public string CuratedType { get => curatedType; set { if(curatedType == value)return; curatedType = value ?? "All"; Changed(); RefreshCards(); } }
    public string CuratedCategory { get => curatedCategory; set { if(curatedCategory == value)return; curatedCategory = value ?? "All"; Changed(); RefreshCards(); } }
    public string CuratedCountText => L.Format("CatalogCountFormat",catalogMatches.Count,CuratedCatalog.All.Count);
    public string CatalogCategorySummary => L.Format("CatalogCategoryCountFormat",CuratedCatalog.All.Select(e=>e.Category).Distinct().Count());
    public int CatalogPageCount => Math.Max(1,(catalogMatches.Count+CatalogPageSize-1)/CatalogPageSize);
    public string CatalogPageText => L.Format("CatalogPageFormat",catalogPage+1,CatalogPageCount);
    public bool CanPreviousCatalogPage => catalogPage > 0;
    public bool CanNextCatalogPage => catalogPage+1 < CatalogPageCount;
    public Visibility GalleryEmptyVisibility => catalogMatches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    void InitializeCuratedCatalog()
    {
        CuratedTypes = new[] { "All" }.Concat(CatalogKindKeys).Select(key=>new CatalogFilterChoice(key,L,key=="All"?"All":"CatalogKind"+key)).ToArray();
        CuratedCategories = new[] { "All" }.Concat(CatalogCategoryKeys).Select(key=>new CatalogFilterChoice(key,L,key=="All"?"All":"CatalogCategory"+key)).ToArray();
        RefreshCards();
    }
    void RefreshCards()
    {
        var criteria = Search+"\u001f"+curatedCategory+"\u001f"+curatedType;
        if(criteria!=lastCatalogCriteria){catalogPage=0;lastCatalogCriteria=criteria; if(GalleryScroll is not null)GalleryScroll.ScrollToTop();}
        catalogMatches=CuratedCatalog.Search(Search,L.Language,curatedCategory,curatedType).ToArray();
        catalogPage=Math.Min(catalogPage,CatalogPageCount-1);
        var categoryMatches=CuratedCatalog.Search(Search,L.Language,"All",curatedType).ToArray();
        var kindMatches=CuratedCatalog.Search(Search,L.Language,curatedCategory,"All").ToArray();
        foreach(var option in CuratedCategories)option.SetCount(option.Key=="All"?categoryMatches.Length:categoryMatches.Count(e=>e.Category.ToString()==option.Key));
        foreach(var option in CuratedTypes)option.SetCount(option.Key=="All"?kindMatches.Length:kindMatches.Count(e=>e.Kind.ToString()==option.Key));
        foreach(var name in new[]{nameof(Cards),nameof(CuratedCountText),nameof(CatalogCategorySummary),nameof(GalleryEmptyVisibility),nameof(CatalogPageText),nameof(CanPreviousCatalogPage),nameof(CanNextCatalogPage)})Changed(name);
    }
    IEnumerable<FeatureCard> CuratedCards()
    {
        foreach(var entry in catalogMatches.Skip(catalogPage*CatalogPageSize).Take(CatalogPageSize))
        {
            var text=CuratedCatalog.Text(entry,L.Language);
            yield return new(null,text.Title,text.Body,L["CatalogCategory"+entry.Category],(ImageSource)FindResource(entry.Illustration+"Illustration"),L["CatalogKind"+entry.Kind],L["LearnMore"]+"  ›",selectedGuide?.Id==entry.Id,L["CatalogKind"+entry.Kind],entry);
        }
    }
    void PreviousCatalogPageClick(object sender,RoutedEventArgs e){if(CanPreviousCatalogPage){catalogPage--;RefreshCards();GalleryScroll.ScrollToTop();}}
    void NextCatalogPageClick(object sender,RoutedEventArgs e){if(CanNextCatalogPage){catalogPage++;RefreshCards();GalleryScroll.ScrollToTop();}}
}
