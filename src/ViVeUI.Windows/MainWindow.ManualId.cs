using System.Windows;
using System.Windows.Input;
using ViVeUI.Core;

namespace ViVeUI.Windows;
public partial class MainWindow
{
    readonly ManualIdInspector idInspector;
    string manualIdText = "";
    bool inspectingId;
    int inspectionVersion;
    public string ManualIdText { get => manualIdText; set { manualIdText = value; Changed(); } }
    public string SelectedProvenanceText => Selected is null ? "" : L[catalog.Any(feature => feature.Id == Selected.Id) ? "InputIdKnown" : "InputIdNotCatalog"];
    public string SelectedAvailabilityText => observationError is not null ? L["ObservationError"] : Selected is not null && !observations.ContainsKey(Selected.Id) ? L["InputIdNotObserved"] : ObservationText;
    async void ManualIdClick(object sender, RoutedEventArgs e) => await InspectManualIdAsync();
    async void ManualIdKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await InspectManualIdAsync();
    }
    async Task InspectManualIdAsync()
    {
        if (busy || closed) return;
        var version = ++inspectionVersion;
        inspectingId = true; Changed(nameof(CanToggle)); Changed(nameof(CanRestoreDefault));
        try
        {
            var result = await idInspector.QueryAsync(ManualIdText);
            if (closed || version != inspectionVersion || result is null || !idInspector.IsCurrent(result)) return;
            ShowPage(ExplorePage);
            // This explicit inspection is independent of visibility filters. It never
            // inserts an ID into the dictionary or silently checks Show unknown items.
            Selected = catalog.FirstOrDefault(feature => feature.Id == result.Id) ?? new Feature(result.Id, L["UnknownId"]);
            toggleActual = new(result.Id, result.Snapshot, result.Error);
            OverrideText = result.Snapshot is null ? L["ToggleUnknownState"] : StateLabel(result.Snapshot);
            foreach (var property in new[] { nameof(ToggleState), nameof(ToggleStateText), nameof(OverrideText), nameof(SelectedProvenanceText), nameof(SelectedAvailabilityText) }) Changed(property);
            detailOpen = true; UpdateLayoutMode();
            if (result.Error is not null) ReportError(result.Error);
            else { lastError = null; Changed(nameof(DiagnosticVisibility)); SetStatus(SelectedProvenanceText); }
        }
        catch (FormatException) { if (!closed && version == inspectionVersion) ReportError(new FormatException(L["InvalidId"])); }
        catch (Exception error) { if (!closed && version == inspectionVersion) ReportError(error); }
        finally
        {
            if (version == inspectionVersion) { inspectingId = false; Changed(nameof(CanToggle)); Changed(nameof(CanRestoreDefault)); }
        }
    }
}
