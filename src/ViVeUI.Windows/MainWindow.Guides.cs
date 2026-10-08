using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public partial class MainWindow
{
    WindowsGuide? selectedGuide;
    string curatedType = "All";
    public WindowsGuide? SelectedGuide => selectedGuide;
    public IReadOnlyList<Choice<string>> CuratedTypes { get; private set; } = [];
    public string CuratedType { get => curatedType; set { curatedType = value ?? "All"; Changed(); RefreshCards(); } }
    public string GuideTitle => selectedGuide is null ? "" : L[selectedGuide.TitleKey];
    public string GuideBody => selectedGuide is null ? "" : L[selectedGuide.BodyKey];
    public string GuideType => selectedGuide is null ? "" : L[selectedGuide.Kind.ToString()];
    public string GuideEvidence => selectedGuide?.Evidence ?? "";
    public string GuideRisk => selectedGuide is null ? "" : L[selectedGuide.RiskKey];
    public string GuideRestore => selectedGuide is null ? "" : L[selectedGuide.RestoreKey];
    public string GuideOpenLabel => L[selectedGuide?.Destination == NativeDestination.Explorer ? "GuideOpenExplorer" : selectedGuide?.Destination == NativeDestination.None ? "GuideSource" : "GuideOpen"];
    public Visibility GuideSourceVisibility => selectedGuide?.Destination == NativeDestination.None ? Visibility.Collapsed : Visibility.Visible;
    public ImageSource? GuideImage => selectedGuide is null ? null : (ImageSource)FindResource(selectedGuide.Illustration + "Illustration");
    public string CuratedCountText => L.Format("CuratedCountFormat", Cards.Count());
    public Visibility GalleryEmptyVisibility => !Cards.Any() ? Visibility.Visible : Visibility.Collapsed;
    public string? LastGuideDestination { get; private set; }
    void RefreshCards() { Changed(nameof(Cards)); Changed(nameof(CuratedCountText)); Changed(nameof(GalleryEmptyVisibility)); }
    void SelectGuide(WindowsGuide? guide)
    {
        selectedGuide = guide;
        foreach(var name in new[] { nameof(SelectedGuide),nameof(GuideTitle),nameof(GuideBody),nameof(GuideType),nameof(GuideEvidence),nameof(GuideRisk),nameof(GuideRestore),nameof(GuideOpenLabel),nameof(GuideImage),nameof(GuideSourceVisibility) }) Changed(name);
        RefreshCards(); Changed(nameof(CanStage));
        if (Root is not null) UpdateLayoutMode();
    }
    IEnumerable<FeatureCard> CuratedCards()
    {
        if (curatedType is "All" or "NativeSettings" or "NativeShortcut" or "InsiderGuide")
            foreach(var guide in GuideCatalog.All.Where(g => (curatedType == "All" || g.Kind.ToString() == curatedType) && (Category == "All" || Category == g.Category) && GuideCatalog.Matches(g,Search,L.Language)))
                yield return new(null,L[guide.TitleKey],L[guide.BodyKey],L[guide.Category],(ImageSource)FindResource(guide.Illustration+"Illustration"),L[guide.Kind.ToString()],L["LearnMore"]+"  ›",selectedGuide?.Key==guide.Key,L[guide.Kind.ToString()],guide);
        if(curatedType is "All" or "HistoricalBadge")
            foreach(var id in new uint[] {37634385,39420424,34300186,36354489,40430431})
            {
                var f=catalog.First(x=>x.Id==id);var title=FeatureEditorial.Title(f,L);var description=FeatureEditorial.Description(f,L);
                if ((Category != "All" && Category != f.Category) || !(string.IsNullOrWhiteSpace(Search) || (title+" "+description+" "+f.Name+" "+Localization.FeatureId(id)).Contains(Search.Trim(),StringComparison.OrdinalIgnoreCase))) continue;
                yield return new(f,title,description,L[f.Category],FeatureImage(f),L["Unverified"],L["LearnMore"]+"  ›",selectedGuide is null && Selected?.Id==id,L["HistoricalBadge"]);
            }
    }
    void GuideSourceClick(object sender,RoutedEventArgs e)
    {
        if(selectedGuide is null)return;
        if(demo){LastGuideDestination=selectedGuide.Source;return;} OpenUrl(selectedGuide.Source);
    }
    void GuideOpenClick(object sender,RoutedEventArgs e) => Safe(() =>
    {
        if(selectedGuide is null)return;
        if(selectedGuide.Destination==NativeDestination.None){GuideSourceClick(sender,e);return;}
        var destination = selectedGuide.Destination==NativeDestination.Explorer ? "explorer.exe" : GuideCatalog.SettingsUri(selectedGuide.Destination) ?? throw new InvalidOperationException("Unsupported guide destination.");
        LastGuideDestination=destination;
        if(!demo)Process.Start(new ProcessStartInfo(destination){UseShellExecute=true});
    });
}
