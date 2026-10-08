using System.ComponentModel;
using System.Windows;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public partial class MainWindow
{
    public string ExecutionNotice => L[ExecutionResults.Any(r=>r.StatusKey!="Applied") ? "RetryReview" : "Restart"];
    public bool CanStage => !busy && Selected is not null && selectedGuide is null;
    void ClearSearchClick(object sender, RoutedEventArgs e) { Search = ""; searchTimer.Stop(); Filter(); }
    void BeginUpdate()
    {
        updateCancellation = new(); updateBusy = true;
        foreach (var name in new[] { nameof(UpdateBusy), nameof(UpdateIdle), nameof(CanDownload) }) Changed(name);
    }
    void EndUpdate()
    {
        updateBusy = false; updateCancellation?.Dispose(); updateCancellation = null;
        foreach (var name in new[] { nameof(UpdateBusy), nameof(UpdateIdle), nameof(CanDownload) }) Changed(name);
    }
    void CancelUpdateClick(object sender, RoutedEventArgs e) => updateCancellation?.Cancel();
    void ConfirmClose(object? sender, CancelEventArgs e)
    {
        if (demo) return;
        if (busy) { e.Cancel = true; SetStatus(L["WaitForApply"]); return; }
        if (Staged.Count > 0 && !LocalizedDialog.Show(this,L,L["Review"],L["ClosePending"],true)) { e.Cancel = true; return; }
        updateCancellation?.Cancel();
    }
}
public sealed record ExecutionRow(uint Id, string StatusKey, string? Error, Locale Locale)
{
    public string Summary => Localization.FeatureId(Id) + " · " + Locale[StatusKey];
    public string Detail => Error is null ? "" : Locale.ErrorSummary(new Exception(Error));
}
