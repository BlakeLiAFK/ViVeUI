using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ViVeUI.Core;

namespace ViVeUI.Windows;
public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    public Locale L { get; } = new();
    readonly IReadOnlyList<Feature> catalog = Catalog.Load();
    readonly IFeatureStore store;
    IReadOnlyList<Feature> discovered = [];
    readonly bool demo;
    readonly string folder;
    readonly UpdateService updater = new();
    readonly DispatcherTimer searchTimer = new() { Interval = TimeSpan.FromMilliseconds(160) };
    Dictionary<uint, string> observations = [];
    string? observationError;
    public ObservableCollection<Change> Staged { get; } = [];
    public IEnumerable<object> ReviewRows => Staged.Select(c => new { c.Id, c.Name, Before = StateLabel(c.Before), After = StateLabel(c.After) });
    public ImageSource? Illustration => Selected?.Illustrated == true ? (ImageSource)FindResource(Selected.Category + "Illustration") : null;
    public ObservableCollection<HistoryRow> History { get; } = [];
    public IReadOnlyList<Feature> Filtered { get; private set; } = [];
    public Dictionary<string, string> Categories => new[] { "All", "Explorer", "Widgets", "System", "Catalog" }.ToDictionary(k => k, k => L[k]);
    public Dictionary<OverrideState, string> States => Enum.GetValues<OverrideState>().ToDictionary(k => k, k => L[k.ToString()]);
    string search = "", category = "All"; Feature? selected; OverrideState desired;
    bool busy, acknowledged, initialized, autoCheck, autoDownload, compact, detailOpen;
    public Visibility CompactVisibility => compact ? Visibility.Visible : Visibility.Collapsed;
    AppRelease? release;
    public string Search { get => search; set { search = value; searchTimer.Stop(); searchTimer.Start(); } }
    public string Category { get => category; set { category = value ?? "All"; Filter(); } }
    public Feature? Selected { get => selected; set { selected = value; Changed(); RefreshDetail(); } }
    public OverrideState Desired { get => desired; set { desired = value; Changed(); } }
    public string ModeText => L[demo ? "Demo" : "Local"];
    public string BuildText => demo ? "Windows 11 · DEMO" : $"Windows {Environment.OSVersion.Version} · {RuntimeInformation.OSArchitecture}";
    public string CountText => $"{Filtered.Count.ToString("N0", CultureInfo.CurrentCulture)} / {catalog.Count.ToString("N0", CultureInfo.CurrentCulture)}";
    public Visibility EmptyVisibility => Filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HistoricalVisibility => Selected?.Illustrated == true ? Visibility.Visible : Visibility.Collapsed;
    public string IllustrationAlt => L[Selected?.Illustrated == true ? "Schematic" : "NoPreview"];
    public string Description => L[Selected?.Illustrated == true ? "Historical" : "UnknownDescription"];
    public string ObservationText => observationError is not null ? L["ObservationError"] : Selected is not null && observations.TryGetValue(Selected.Id, out var observed) ? observed : L["NotObserved"];
    public string OverrideText { get; private set; } = "";
    public string QueueText => Staged.Count == 0 ? L["NoChanges"] : $"{Staged.Count} {L["Staged"]}";
    public string StatusText { get; private set; } = "";
    public bool Acknowledged { get => acknowledged; set { acknowledged = value; Changed(); Changed(nameof(CanApply)); } }
    public bool CanApply => !busy && Acknowledged && Staged.Count > 0;
    public bool NotBusy => !busy;
    public bool CanDownload => !busy && release is not null;
    public string UpdateText => L[updater.Status.ToString()];
    public string UpdateDetails { get; private set; } = "";
    public double DownloadProgress { get; private set; }
    public bool AutoCheck { get => autoCheck; set { autoCheck = value; Changed(); SaveSettings(); } }
    public bool AutoDownload { get => autoDownload; set { autoDownload = value; Changed(); SaveSettings(); } }
    public MainWindow(bool demo)
    {
        this.demo = demo; store = demo ? new DemoStore() : new WindowsStore();
        folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), demo ? "ViVeUI-Demo" : "ViVeUI");
        InitializeComponent(); DataContext = this;
        searchTimer.Tick += (_, _) => { searchTimer.Stop(); Filter(); };
        Staged.CollectionChanged += (_, _) => { Acknowledged = false; Changed(nameof(QueueText)); Changed(nameof(ReviewRows)); Changed(nameof(CanApply)); };
        LoadSettings(); initialized = true; ApplyContrast(); Filter(); LoadHistory();
        SizeChanged += (_, _) => UpdateLayoutMode();
        SystemParameters.StaticPropertyChanged += SystemPreferenceChanged;
        Loaded += async (_, _) => { await RefreshObservations(); if (AutoCheck && !demo) await CheckUpdates(); };
        Closed += (_, _) => { searchTimer.Stop(); updater.Dispose(); SystemParameters.StaticPropertyChanged -= SystemPreferenceChanged; };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.F && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) { ShowPage(ExplorePage); SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; } };
    }
    void SystemPreferenceChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.Invoke(ApplyContrast); }
    void ApplyContrast()
    {
        if (!SystemParameters.HighContrast) { foreach (var key in new[] { "SidebarBrush", "SidebarTextBrush", "SidebarMutedBrush", "CanvasBrush", "PaperBrush", "InkBrush", "MutedBrush", "LineBrush", "AccentBrush" }) Resources.Remove(key); return; }
        Resources["SidebarBrush"] = SystemColors.WindowBrush; Resources["SidebarTextBrush"] = SystemColors.WindowTextBrush; Resources["SidebarMutedBrush"] = SystemColors.WindowTextBrush;
        Resources["CanvasBrush"] = SystemColors.WindowBrush; Resources["PaperBrush"] = SystemColors.WindowBrush;
        Resources["InkBrush"] = SystemColors.WindowTextBrush; Resources["MutedBrush"] = SystemColors.WindowTextBrush;
        Resources["LineBrush"] = SystemColors.WindowTextBrush; Resources["AccentBrush"] = SystemColors.HighlightBrush;
    }
    void UpdateLayoutMode()
    {
        var scale = Root.LayoutTransform is ScaleTransform t ? t.ScaleX : 1;
        var next = ActualWidth / scale < 1120;
        if (next != compact) detailOpen = false;
        compact = next; Changed(nameof(CompactVisibility));
        Grid.SetColumn(DetailPane, compact ? 0 : 2); Grid.SetColumnSpan(DetailPane, compact ? 3 : 1);
        Grid.SetColumnSpan(CatalogPane, compact ? 3 : 1);
        CatalogPane.Visibility = compact && detailOpen ? Visibility.Collapsed : Visibility.Visible;
        DetailPane.Visibility = !compact || detailOpen ? Visibility.Visible : Visibility.Collapsed;
    }
    void OpenDetailClick(object sender, RoutedEventArgs e) { detailOpen = true; UpdateLayoutMode(); }
    void BackClick(object sender, RoutedEventArgs e) { detailOpen = false; UpdateLayoutMode(); }
    void Filter()
    {
        Filtered = Catalog.Search(catalog.Concat(discovered), search, category).ToArray(); Changed(nameof(Filtered)); Changed(nameof(CountText)); Changed(nameof(EmptyVisibility));
        if (Selected is null || !Filtered.Contains(Selected)) Selected = Filtered.FirstOrDefault();
    }
    string StateLabel(Snapshot snapshot) => !snapshot.Exists ? L["Default"] : L[snapshot.State.ToString()];
    void RefreshDetail()
    {
        try { var state = Selected is null ? Snapshot.Default : store.Read(Selected.Id); OverrideText = StateLabel(state); Desired = state.State; }
        catch (Exception e) { OverrideText = L["Error"]; SetStatus(L["Diagnostics"] + ": " + e.Message); }
        foreach (var name in new[] { nameof(Illustration), nameof(OverrideText), nameof(ObservationText), nameof(IllustrationAlt), nameof(Description), nameof(HistoricalVisibility) }) Changed(name);
    }
    async Task RefreshObservations()
    {
        try
        {
            if (demo) observations = new() { [37634385] = "Enabled · DEMO", [39420424] = "Default · DEMO" };
            else { if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18963)) throw new PlatformNotSupportedException("Windows build 18963 or newer required."); observations = await Task.Run(WindowsStore.Observe); }
            observationError = null;
            var known = catalog.Select(f => f.Id).ToHashSet();
            discovered = observations.Keys.Where(id => !known.Contains(id)).Select(id => new Feature(id, L["UnknownId"])).ToArray();
            Filter();
        }
        catch (Exception e) { observationError = e.Message; SetStatus(L["ObservationError"] + ": " + e.Message); }
        RefreshDetail();
    }
    void SetStatus(string text) { StatusText = text; Changed(nameof(StatusText)); }
    void SetBusy(bool value) { busy = value; Changed(nameof(NotBusy)); Changed(nameof(CanApply)); Changed(nameof(CanDownload)); }
    void ShowPage(UIElement page) { foreach (var p in new UIElement[] { ExplorePage, ChangesPage, UpdatesPage, SettingsPage }) p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed; }
    void ExploreClick(object sender, RoutedEventArgs e) => ShowPage(ExplorePage);
    void ChangesClick(object sender, RoutedEventArgs e) => ShowPage(ChangesPage);
    void UpdatesClick(object sender, RoutedEventArgs e) => ShowPage(UpdatesPage);
    void SettingsClick(object sender, RoutedEventArgs e) => ShowPage(SettingsPage);
    void StageClick(object sender, RoutedEventArgs e) => Safe(() => Stage());
    void Stage()
    {
        if (busy || Selected is null) return;
        var before = store.Read(Selected.Id); var after = Desired == OverrideState.Default ? Snapshot.Default : new Snapshot(true, Desired);
        if (before == after) { SetStatus(L["Unchanged"]); return; }
        var existing = Staged.FirstOrDefault(c => c.Id == Selected.Id); if (existing is not null) Staged.Remove(existing);
        if (Staged.Count >= 100) throw new InvalidOperationException("Review is limited to 100 changes.");
        Staged.Add(new(Selected.Id, Selected.Name, before, after)); SetStatus(QueueText);
    }
    void InspectClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (!uint.TryParse(Search, out var id) || id == 0) throw new InvalidDataException(L["InvalidId"]);
        Selected = catalog.FirstOrDefault(x => x.Id == id) ?? new(id, L["UnknownId"]);
    });
    void ClearClick(object sender, RoutedEventArgs e) { if (!busy) Staged.Clear(); }
    async void ApplyClick(object sender, RoutedEventArgs e) { if (CanApply) await ApplyStaged(); }
    async Task ApplyStaged()
    {
        SetBusy(true);
        var receipt = new Receipt(Guid.NewGuid(), DateTimeOffset.Now, BuildText, Staged.ToList());
        try
        {
            ChangeEngine.Validate(receipt.Changes);
            // Persist intentions and exact snapshots BEFORE requesting elevation.
            SaveReceipt(receipt);
            var response = demo ? new WorkerResponse(ChangeEngine.Apply(store, receipt.Changes), null) : await Program.ElevateAsync(receipt.Changes, L.Language);
            if (response.Error is not null) throw new InvalidOperationException(response.Error);
            receipt = receipt with { Results = response.Results }; SaveReceipt(receipt);
            Staged.Clear();
            var all = response.Results?.Count == receipt.Changes.Count && response.Results.All(r => r.Applied);
            SetStatus(all ? L["Restart"] : L["Partial"]);
        }
        catch (Exception e) { SetStatus(L["Error"] + ": " + e.Message); }
        finally { LoadHistory(); RefreshDetail(); SetBusy(false); }
    }
    void SaveReceipt(Receipt receipt)
    {
        Directory.CreateDirectory(Path.Combine(folder, "history"));
        var path = Path.Combine(folder, "history", receipt.Id + ".json");
        AtomicWrite(path, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
    }
    static void AtomicWrite(string path, string text)
    {
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { using var writer = new StreamWriter(stream, leaveOpen: true); writer.Write(text); writer.Flush(); stream.Flush(true); }
        File.Move(temp, path, true);
    }
    void LoadHistory()
    {
        History.Clear(); var dir = Path.Combine(folder, "history"); if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(100))
        {
            try { var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(file)); if (receipt is not null) History.Add(new(receipt, L)); }
            catch (Exception e) { SetStatus(L["Diagnostics"] + ": " + Path.GetFileName(file) + " — " + e.Message); }
        }
    }
    void UndoClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (busy || HistoryList.SelectedItem is not HistoryRow row) return;
        var proposed = new List<Change>();
        foreach (var change in row.Receipt.Changes)
        {
            var current = store.Read(change.Id);
            if (current == change.Before) continue; // Not applied, or already restored.
            proposed.Add(ChangeEngine.Undo(change, current)); // Conflict stops the whole staging operation.
        }
        if (proposed.Count == 0) { SetStatus(L["Unchanged"]); return; }
        Staged.Clear(); foreach (var change in proposed) Staged.Add(change); ShowPage(ChangesPage);
    });
    void Safe(Action action) { try { action(); } catch (Exception e) { SetStatus(L["Error"] + ": " + e.Message); } }
    void SourceClick(object sender, RoutedEventArgs e) => OpenUrl(Catalog.Source);
    void HistoricalClick(object sender, RoutedEventArgs e)
    {
        var path = Selected?.Id switch { 37634385 or 36354489 => "2022/06/09/announcing-windows-11-insider-preview-build-25136/", 34300186 => "2022/09/14/announcing-windows-11-insider-preview-build-25201/", 39420424 => "2022/11/10/announcing-windows-11-insider-preview-build-22621-891-and-22623-891/", 40430431 => "2023/01/12/announcing-windows-11-insider-preview-build-25276/", _ => null };
        if (path is not null) OpenUrl("https://blogs.windows.com/windows-insider/" + path);
    }
    void OpenUrl(string url) => Safe(() => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }));
    async void RefreshClick(object sender, RoutedEventArgs e) => await RefreshObservations();
    async void CheckClick(object sender, RoutedEventArgs e) => await CheckUpdates();
    async Task CheckUpdates()
    {
        if (busy) return; SetBusy(true); UpdateDetails = ""; Changed(nameof(UpdateDetails));
        try
        {
            var pending = updater.CheckAsync(new Version(0, 1, 0), RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64"); Changed(nameof(UpdateText));
            release = await pending; UpdateDetails = release is null ? "" : $"{release.Version} · {release.Asset.Size / 1024 / 1024} MB\n{release.Page}";
        }
        catch (Exception e) { release = null; UpdateDetails = L["Diagnostics"] + ": " + e.Message; }
        finally { SetBusy(false); Changed(nameof(UpdateText)); Changed(nameof(UpdateDetails)); }
        if (release is not null && AutoDownload) await Download();
    }
    async void DownloadClick(object sender, RoutedEventArgs e) => await Download();
    async Task Download()
    {
        if (release is null || busy) return; SetBusy(true); DownloadProgress = 0; Changed(nameof(DownloadProgress));
        try
        {
            var pending = updater.DownloadAsync(release.Asset, Path.Combine(folder, "downloads"), new Progress<double>(p => { DownloadProgress = p * 100; Changed(nameof(DownloadProgress)); })); Changed(nameof(UpdateText));
            var path = await pending; UpdateDetails = path + "\nSHA-256: " + release.Asset.Sha256;
        }
        catch (Exception e) { UpdateDetails = L["Diagnostics"] + ": " + e.Message; }
        finally { SetBusy(false); Changed(nameof(UpdateText)); Changed(nameof(UpdateDetails)); }
    }
    void OpenDownloadsClick(object sender, RoutedEventArgs e) => Safe(() => { var path = Path.Combine(folder, "downloads"); Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); });
    void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox?.SelectedItem is not ComboBoxItem item) return;
        L.Set(item.Tag?.ToString() ?? "en"); CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(L.Language == "zh" ? "zh-CN" : L.Language);
        foreach (var name in new[] { nameof(Categories), nameof(States), nameof(ModeText), nameof(QueueText), nameof(ReviewRows), nameof(UpdateText), nameof(CountText) }) Changed(name);
        RefreshDetail(); LoadHistory(); SaveSettings();
    }
    void ScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Root is not null) { Root.LayoutTransform = new ScaleTransform(e.NewValue, e.NewValue); if (initialized) UpdateLayoutMode(); } }
    void SaveSettings()
    {
        if (!initialized) return;
        Safe(() => { Directory.CreateDirectory(folder); AtomicWrite(Path.Combine(folder, "settings.json"), JsonSerializer.Serialize(new Preferences(L.Language, AutoCheck, AutoDownload))); });
    }
    void LoadSettings()
    {
        try
        {
            var path = Path.Combine(folder, "settings.json"); if (!File.Exists(path)) return;
            var prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)); if (prefs is null) return;
            autoCheck = prefs.AutoCheck; autoDownload = prefs.AutoDownload;
            LanguageBox.SelectedIndex = prefs.Language == "zh" ? 1 : prefs.Language == "es" ? 2 : 0;
        }
        catch (Exception e) { SetStatus(L["Diagnostics"] + ": " + e.Message); }
    }
    public async Task SmokeAsync()
    {
        if (!demo || store is not DemoStore) throw new InvalidOperationException("Smoke tests require fake backend.");
        Search = "37634385"; Filter(); if (Filtered.Count != 1) throw new Exception("Search failed.");
        Selected = Filtered[0]; Desired = OverrideState.Enabled; Stage(); if (Staged.Count != 1) throw new Exception("Staging failed.");
        ShowPage(ExplorePage); await Capture("explore.png");
        ShowPage(ChangesPage); await Capture("review.png");
        Acknowledged = true; await ApplyStaged(); if (store.Read(37634385).State != OverrideState.Enabled) throw new Exception("Fake apply failed.");
        HistoryList.SelectedIndex = 0; UndoClick(this, new()); if (Staged.Count != 1 || Staged[0].After != Snapshot.Default) throw new Exception("Undo failed.");
        await ApplyStaged(); if (store.Read(37634385) != Snapshot.Default) throw new Exception("Fake undo failed.");
        LanguageBox.SelectedIndex = 1; ShowPage(ExplorePage); await Capture("explore-zh.png");
        LanguageBox.SelectedIndex = 2; ShowPage(SettingsPage); await Capture("settings-es.png");
        ShowPage(UpdatesPage); await Capture("updates.png");
        LanguageBox.SelectedIndex = 0; Width = 900; ShowPage(ExplorePage); UpdateLayoutMode();
        await Capture("compact-catalog.png"); OpenDetailClick(this, new()); await Capture("compact-detail.png");
        File.WriteAllText("smoke-result.txt", "PASS: WPF startup, catalog search, selection, stage, fake apply/readback, scoped undo, language switch, 7 rendered previews. No Windows settings were modified.");
    }
    async Task Capture(string filename)
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        Directory.CreateDirectory("previews");
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine("previews", filename)); encoder.Save(stream);
    }
}
public sealed record Preferences(string Language, bool AutoCheck, bool AutoDownload);
public sealed class HistoryRow(Receipt receipt, Locale locale)
{
    public Receipt Receipt { get; } = receipt;
    public string Summary => $"{Receipt.At:g} · {Receipt.Changes.Count} · {locale[Receipt.Results is null ? "Pending" : Receipt.Results.Count == Receipt.Changes.Count && Receipt.Results.All(r => r.Applied) ? "Applied" : "Partial"]}";
    public string Detail => string.Join("\n", Receipt.Changes.Select(c => $"{c.Id}: {locale[c.Before.Exists ? c.Before.State.ToString() : "Default"]} → {locale[c.After.Exists ? c.After.State.ToString() : "Default"]}")) + (Receipt.Results is null ? "" : "\n" + string.Join("\n", Receipt.Results.Where(r => r.Error is not null).Select(r => r.Error)));
}
