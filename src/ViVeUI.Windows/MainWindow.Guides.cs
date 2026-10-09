using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public partial class MainWindow
{
    CuratedEntry? selectedGuide;
    public CuratedEntry? SelectedGuide => selectedGuide;
    public string GuideTitle => selectedGuide is null ? "" : CuratedCatalog.Text(selectedGuide,L.Language).Title;
    public string GuideBody => selectedGuide is null ? "" : CuratedCatalog.Text(selectedGuide,L.Language).Body;
    public string GuideType => selectedGuide is null ? "" : L["CatalogKind"+selectedGuide.Kind];
    public string GuideEvidence => selectedGuide is null ? "" : CuratedCatalog.Text(selectedGuide,L.Language).Evidence;
    public string GuideRisk => selectedGuide is null ? "" : L["CatalogRisk"+selectedGuide.Risk];
    public string GuideRestore => selectedGuide is null ? "" : L["CatalogRestore"+selectedGuide.Restore];
    public string GuideRestart => selectedGuide is null ? "" : L["CatalogRestart"+selectedGuide.Restart];
    public string GuideReferenceIds => selectedGuide is null ? "" : string.Join(" · ",selectedGuide.FeatureIds.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    public Visibility GuideReferenceVisibility => selectedGuide?.FeatureIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public IEnumerable<string> GuideSources => selectedGuide?.Sources ?? [];
    public string GuideOpenLabel => L[selectedGuide?.Destination == "explorer.exe" ? "GuideOpenExplorer" : string.IsNullOrEmpty(selectedGuide?.Destination) ? "GuideSource" : "GuideOpen"];
    public bool CanOpenGuide => selectedGuide is not null && (!string.IsNullOrEmpty(selectedGuide.Destination) || selectedGuide.Sources.Count > 0);
    public ImageSource? GuideImage => selectedGuide is null ? null : (ImageSource)FindResource(selectedGuide.Illustration + "Illustration");
    public string? LastGuideDestination { get; private set; }
    void SelectGuide(CuratedEntry? guide)
    {
        selectedGuide = guide;
        foreach(var name in new[] { nameof(SelectedGuide),nameof(GuideTitle),nameof(GuideBody),nameof(GuideType),nameof(GuideEvidence),nameof(GuideRisk),nameof(GuideRestore),nameof(GuideRestart),nameof(GuideOpenLabel),nameof(GuideImage),nameof(GuideSources),nameof(GuideReferenceIds),nameof(GuideReferenceVisibility),nameof(CanOpenGuide) }) Changed(name);
        RefreshCards(); Changed(nameof(CanToggle)); Changed(nameof(CanRestoreDefault));
        if (Root is not null) UpdateLayoutMode();
    }
    void GuideSourceClick(object sender,RoutedEventArgs e) => Safe(() =>
    {
        var source = sender is Button { Tag: string url } ? url : selectedGuide?.Sources.FirstOrDefault();
        if(source is null || selectedGuide is null || !selectedGuide.Sources.Contains(source) || !CuratedCatalog.IsAllowedSource(source)) return;
        LastGuideDestination=source;
        if(!demo) OpenUrl(source);
    });
    void GuideOpenClick(object sender,RoutedEventArgs e) => Safe(() =>
    {
        if(selectedGuide is null)return;
        if(string.IsNullOrEmpty(selectedGuide.Destination)){GuideSourceClick(sender,e);return;}
        var destination = selectedGuide.Destination;
        if(!CuratedCatalog.IsAllowedDestination(destination)) throw new InvalidOperationException("Unsupported guide destination.");
        LastGuideDestination=destination;
        if(!demo)Process.Start(new ProcessStartInfo(destination){UseShellExecute=true});
    });
}
