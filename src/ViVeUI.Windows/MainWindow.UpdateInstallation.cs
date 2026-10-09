using System.Windows;
using System.Windows.Controls;
using ViVeUI.Core;

namespace ViVeUI.Windows;

public partial class MainWindow
{
    bool installingUpdate, updateInstalled;
    public bool CanInstallUpdate => !demo && !closed && !busy && !updateBusy && !updateInstalled && release is not null;
    async void InstallUpdateClick(object sender, RoutedEventArgs e)
    {
        if (!CanInstallUpdate || release is null) return;
        var selectedRelease = release;
        installingUpdate = true;
        BeginUpdate();
        try
        {
            SetStatus(L["UpdateInstalling"]);
            await updater.DownloadAsync(selectedRelease.Asset, Path.Combine(folder, "downloads"),
                new Progress<double>(p => { DownloadProgress = p * 100; Changed(nameof(DownloadProgress)); }), updateCancellation!.Token);
            await using var session = await SelfUpdateWorker.PrepareAsync(selectedRelease, updateCancellation.Token);
            updateInstalled = true;
            UpdateDetails = session.Prepared.TargetPath + "\nSHA-256: " + session.Prepared.Sha256;
            Changed(nameof(UpdateDetails)); Changed(nameof(UpdateText));
            SetStatus(L["UpdateInstalled"]);
            var dialog = LocalizedDialog.Create(this, L, L["UpdateRestartTitle"], L["UpdateRestartPrompt"], confirm: true);
            var buttons = DescendantsLogical(dialog).OfType<Button>().ToArray();
            foreach (var button in buttons) button.Content = L[button.IsCancel ? "UpdateLater" : "UpdateRestartNow"];
            if (dialog.ShowDialog() == true)
            {
                await session.RestartAsync();
                installingUpdate = false;
                Application.Current.Shutdown();
            }
        }
        catch (OperationCanceledException) { SetStatus(L["Canceled"]); }
        catch (Exception error) { updateInstalled = false; ReportError(error); }
        finally { installingUpdate = false; EndUpdate(); Changed(nameof(UpdateText)); Changed(nameof(CanInstallUpdate)); }
    }
    static IEnumerable<DependencyObject> DescendantsLogical(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in DescendantsLogical(child)) yield return descendant;
        }
    }
}
