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
    public IReadOnlyList<LanguageChoice> Languages { get; }
    public string LanguagePreference { get; set; } = "system";
    Exception? lastError;
    public Visibility DiagnosticVisibility => lastError is null ? Visibility.Collapsed : Visibility.Visible;
    public string TransitionArrow => L.Direction == FlowDirection.RightToLeft ? "←" : "→";
    readonly IReadOnlyList<Feature> catalog = Catalog.Load();
    readonly IFeatureStore store;
    readonly ImmediateToggleController toggle;
    ImmediateToggleState? toggleActual;
    Receipt? activeReceipt;
    public bool? ToggleState => toggleActual?.State;
    bool SupportsOverrides => demo || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18963);
    public bool CanToggle => SupportsOverrides && !busy && !refreshing && !inspectingId && selectedGuide is null && Selected is not null && toggleActual?.ReadSucceeded == true && !CuratedCatalog.IsHistoricalReference(Selected.Id);
    public bool CanRestoreDefault => SupportsOverrides && !busy && !refreshing && !inspectingId && selectedGuide is null && Selected is not null && toggleActual?.ReadSucceeded == true && toggleActual.Snapshot != Snapshot.Default && !CuratedCatalog.IsHistoricalReference(Selected.Id);
    public string ToggleStateText => L[busy ? "ToggleBusy" : !SupportsOverrides ? "FailureUnsupported" : Selected is not null && CuratedCatalog.IsHistoricalReference(Selected.Id) ? "ToggleReadOnly" : toggleActual?.ReadSucceeded != true ? "ToggleUnknownState" : ToggleState is null ? "ToggleDefaultState" : ToggleState == true ? "Enabled" : "Disabled"];
    IReadOnlyList<Feature> discovered = [];
    readonly bool demo;
    readonly string folder;
    readonly UpdateService updater;
    CancellationTokenSource? updateCancellation;
    bool updateBusy;
    public bool UpdateIdle => !updateBusy;
    public bool UpdateBusy => updateBusy;
    public ObservableCollection<ExecutionRow> ExecutionResults { get; } = [];
    public Visibility ExecutionVisibility => ExecutionResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    readonly DispatcherTimer searchTimer = new() { Interval = TimeSpan.FromMilliseconds(160) };
    Dictionary<uint, string> observations = [];
    string? observationError;
    string? statusKey;
    public IEnumerable<FeatureCard> Cards => CuratedCards();
    ImageSource? FeatureImage(Feature f) => f.Illustrated ? (ImageSource)FindResource((FeatureEditorial.Key(f.Id) == "Navigation" ? "Navigation" : f.Category) + "Illustration") : null;
    public string SelectedTitle => Selected is null ? L["Detail"] : FeatureEditorial.Title(Selected, L);
    public string SelectedDescription
    {
        get
        {
            if (Selected is null) return L["UnknownDescription"];
            var references = CuratedCatalog.All.Where(e => e.Kind == CuratedKind.Historical && e.FeatureIds.Contains(Selected.Id))
                .Select(e => { var text = CuratedCatalog.Text(e, L.Language); return text.Title + "\n" + text.Body + "\n" + text.Evidence; }).ToArray();
            return references.Length > 0 ? string.Join("\n\n", references) : FeatureEditorial.Description(Selected, L);
        }
    }
    public string VersionText => "ViVeUI " + BuildInfo.VersionText;
    string currentPage = "Explore";
    public bool IsExplore => currentPage == "Explore";
    public bool IsChanges => currentPage == "Changes";
    public bool IsUpdates => currentPage == "Updates";
    public bool IsSettings => currentPage == "Settings";
    public Visibility SearchPlaceholderVisibility => string.IsNullOrEmpty(Search) ? Visibility.Visible : Visibility.Collapsed;
    public ImageSource? Illustration => Selected is not null ? FeatureImage(Selected) : null;
    public ObservableCollection<HistoryRow> History { get; } = [];
    public IReadOnlyList<Feature> Filtered { get; private set; } = [];
    string search = "", category = "All"; Feature? selected;
    bool busy, refreshing, closed, initialized, autoCheck, autoDownload, compact, detailOpen;
    public Visibility CompactVisibility => compact ? Visibility.Visible : Visibility.Collapsed;
    AppRelease? release;
    public string Search { get => search; set { search = value; Changed(); Changed(nameof(SearchPlaceholderVisibility)); searchTimer.Stop(); searchTimer.Start(); } }
    public string Category { get => category; set { category = value ?? "All"; Changed(); Filter(); } }
    public Feature? Selected { get => selected; set { selected = value; if (value is not null && selectedGuide is not null) SelectGuide(null); Changed(); Changed(nameof(Cards)); Changed(nameof(CanToggle)); RefreshDetail(); } }
    public string ModeText => L[demo ? "Demo" : "Local"];
    public string BuildText => demo ? "Windows 11 · " + L["DemoShort"] : $"Windows {Environment.OSVersion.Version} · {RuntimeInformation.OSArchitecture}";
    public string CountText => $"{Filtered.Count.ToString("N0", CultureInfo.CurrentCulture)} / {catalog.Count.ToString("N0", CultureInfo.CurrentCulture)}";
    public Visibility EmptyVisibility => Filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HistoricalImageVisibility => Selected?.Illustrated == true ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HistoricalVisibility => Selected is not null && CuratedCatalog.IsHistoricalReference(Selected.Id) ? Visibility.Visible : Visibility.Collapsed;
    public string IllustrationAlt => L[Selected?.Illustrated == true ? "Schematic" : "NoPreview"];
    public string Description => L[Selected is not null && CuratedCatalog.IsHistoricalReference(Selected.Id) ? "Historical" : "UnknownDescription"];
    public string ObservationText => observationError is not null ? L["ObservationError"] : Selected is not null && observations.TryGetValue(Selected.Id, out var observed) ? LocalizeObservation(observed) : L["NotObserved"];
    public string OverrideText { get; private set; } = "";
    public string StatusText { get; private set; } = "";
    public bool NotBusy => !busy;
    public bool CanDownload => !updateBusy && release is not null;
    public string UpdateText => L[updater.Status.ToString()];
    public string UpdateDetails { get; private set; } = "";
    public double DownloadProgress { get; private set; }
    public bool AutoCheck { get => autoCheck; set { autoCheck = value; Changed(); SaveSettings(); } }
    public bool AutoDownload { get => autoDownload; set { autoDownload = value; Changed(); SaveSettings(); } }
    public MainWindow(bool demo, string? testFolder = null, UpdateService? testUpdater = null)
    {
        updater = testUpdater ?? new UpdateService();
        this.demo = demo; store = demo ? new DemoStore() : new WindowsStore();
        toggle = new(store, ExecuteImmediateAsync);
        idInspector = new((id, token) => Task.Run(() => store.Read(id), token));
        folder = testFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), demo ? "ViVeUI-Demo" : "ViVeUI");
        Languages = new[] { new LanguageChoice("system", "", L) }.Concat(Localization.Languages.Select(l => new LanguageChoice(l.Code, l.NativeName, L))).ToArray();
        InitializeCuratedCatalog();
        InitializeComponent(); DataContext = this;
        searchTimer.Tick += (_, _) => { searchTimer.Stop(); Filter(); };
        LoadSettings(); L.Set(LanguagePreference); initialized = true; ApplyContrast(); Filter(); LoadHistory(); SelectGuide(CuratedCatalog.All[0]);
        SizeChanged += (_, _) => UpdateLayoutMode();
        SystemParameters.StaticPropertyChanged += SystemPreferenceChanged;
        Loaded += async (_, _) => { await RefreshObservations(); if (!closed && AutoCheck && !demo) await CheckUpdates(); };
        Closing += ConfirmClose;
        Closed += (_, _) => { closed = true; idInspector.Dispose(); searchTimer.Stop(); updateCancellation?.Cancel(); updater.Dispose(); SystemParameters.StaticPropertyChanged -= SystemPreferenceChanged; };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.F && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) { ShowPage(ExplorePage); SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; } };
    }
    void SystemPreferenceChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.Invoke(ApplyContrast); }
    void ThemeChanged(object sender, SelectionChangedEventArgs e) { if (initialized) { ApplyContrast(); SaveSettings(); } }
    void ApplyContrast()
    {
        if (!SystemParameters.HighContrast) { foreach (var key in new[] { "SidebarBrush", "SidebarTextBrush", "SidebarMutedBrush", "CanvasBrush", "PaperBrush", "InkBrush", "MutedBrush", "LineBrush", "AccentBrush", "AccentTextBrush", "SelectionBrush" }) Resources.Remove(key);
            if (ThemeBox?.SelectedIndex == 2 || (ThemeBox?.SelectedIndex == 0 && Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int light && light == 0)) { var colors = new Dictionary<string,string> { ["CanvasBrush"]="#151D2B", ["PaperBrush"]="#202C3E", ["SidebarBrush"]="#192333", ["InkBrush"]="#EDF3FC", ["MutedBrush"]="#BCCBE0", ["LineBrush"]="#43516A", ["AccentBrush"]="#89AEFF", ["AccentTextBrush"]="#12213B", ["SelectionBrush"]="#30476B" }; foreach (var item in colors) Resources[item.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.Value)); } return; }
        Resources["SidebarBrush"] = SystemColors.WindowBrush; Resources["SidebarTextBrush"] = SystemColors.WindowTextBrush; Resources["SidebarMutedBrush"] = SystemColors.WindowTextBrush;
        Resources["CanvasBrush"] = SystemColors.WindowBrush; Resources["PaperBrush"] = SystemColors.WindowBrush;
        Resources["InkBrush"] = SystemColors.WindowTextBrush; Resources["MutedBrush"] = SystemColors.WindowTextBrush;
        Resources["SelectionBrush"] = SystemColors.HighlightBrush; Resources["LineBrush"] = SystemColors.WindowTextBrush; Resources["AccentBrush"] = SystemColors.HighlightBrush; Resources["AccentTextBrush"] = SystemColors.HighlightTextBrush;
    }
    void UpdateLayoutMode()
    {
        var scale = Root.LayoutTransform is ScaleTransform t ? t.ScaleX : 1;
        var viewportWidth = double.IsNaN(Root.Width) ? ActualWidth : Root.Width;
        var next = viewportWidth / scale < 1120;
        if (next != compact) detailOpen = false;
        compact = next; Changed(nameof(CompactVisibility));
        ExplorerColumns.ColumnDefinitions[0].MinWidth = compact ? 0 : 260;
        ExplorerColumns.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 20);
        ExplorerColumns.ColumnDefinitions[2].Width = new GridLength(compact ? 0 : 438);
        Grid.SetColumn(DetailPane, compact ? 0 : 2); Grid.SetColumnSpan(DetailPane, compact ? 3 : 1);
        Grid.SetColumnSpan(CatalogPane, compact ? 3 : 1);
        CatalogPane.Visibility = compact && detailOpen ? Visibility.Collapsed : Visibility.Visible;
        DetailPane.Visibility = (!compact || detailOpen) && selectedGuide is null ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(GuideDetailPane,compact ? 0 : 2); Grid.SetColumnSpan(GuideDetailPane,compact ? 3 : 1);
        GuideDetailPane.Visibility = (!compact || detailOpen) && selectedGuide is not null ? Visibility.Visible : Visibility.Collapsed;
    }
    void OpenDetailClick(object sender, RoutedEventArgs e) { detailOpen = true; UpdateLayoutMode(); }
    void BackClick(object sender, RoutedEventArgs e) { detailOpen = false; UpdateLayoutMode(); }
    void Filter()
    {
        var editorial = catalog.Where(f => FeatureEditorial.Key(f.Id) is not null).ToDictionary(f => f.Id, f => FeatureEditorial.Title(f,L) + " " + FeatureEditorial.Description(f,L));
        Filtered = Catalog.Search(catalog.Concat(discovered), search, category, editorial).ToArray(); Changed(nameof(Filtered)); Changed(nameof(CountText)); Changed(nameof(EmptyVisibility));
        RefreshCards();
        if (Selected is not null && !IsFeatureVisible(Selected.Id)) Selected = null;
        if (selectedGuide is not null && !IsGuideVisible(selectedGuide.Id)) SelectGuide(null);
    }
    string StateLabel(Snapshot snapshot) => L.State(snapshot);
    void RefreshDetail()
    {
        toggleActual = Selected is null ? null : toggle.Read(Selected.Id);
        OverrideText = toggleActual?.ReadSucceeded == true ? StateLabel(toggleActual.Snapshot!) : L["ToggleUnknownState"];
        foreach (var property in new[] { nameof(ToggleState), nameof(ToggleStateText), nameof(CanToggle), nameof(CanRestoreDefault) }) Changed(property);
        foreach (var name in new[] { nameof(SelectedTitle), nameof(SelectedDescription), nameof(Illustration), nameof(OverrideText), nameof(ObservationText), nameof(IllustrationAlt), nameof(Description), nameof(HistoricalVisibility), nameof(HistoricalImageVisibility), nameof(SelectedProvenanceText), nameof(SelectedAvailabilityText) }) Changed(name);
    }
    async Task RefreshObservations()
    {
        if (busy || refreshing || closed) return;
        refreshing = true; RefreshDetail();
        try
        {
            if (demo) observations = new() { [37634385] = "Enabled · Demo", [39420424] = "Default · Demo" };
            else { if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18963)) throw new PlatformNotSupportedException("Windows build 18963 or newer required."); observations = await Task.Run(WindowsStore.Observe); }
            observationError = null;
            var known = catalog.Select(f => f.Id).ToHashSet();
            discovered = observations.Keys.Where(id => !known.Contains(id)).Select(id => new Feature(id, L["UnknownId"])).ToArray();
            Filter();
        }
        catch (Exception e) { observationError = e.Message; ReportError(e); SetStatus(L["ObservationError"]); }
        refreshing = false;
        if (!closed) RefreshDetail();
    }
    void SetStatus(string text) { statusKey = Localization.English.Keys.FirstOrDefault(key => L[key] == text); StatusText = text; Changed(nameof(StatusText)); }
    void SetBusy(bool value) { busy = value; Changed(nameof(NotBusy)); Changed(nameof(CanRestoreDefault)); Changed(nameof(ToggleStateText)); Changed(nameof(CanDownload)); Changed(nameof(CanToggle)); }
    string LocalizeObservation(string observed)
    {
        var parts = observed.Split(" · ");
        var state = parts.Length > 0 && Localization.English.ContainsKey("Observed_" + parts[0]) ? L["Observed_" + parts[0]] : L["NotObserved"];
        var priority = parts.Length > 1 && Localization.English.ContainsKey("Priority_" + parts[1]) ? L["Priority_" + parts[1]] : L["Priority_Unknown"];
        return state + " · " + priority;
    }
    void ShowPage(UIElement page)
    {
        foreach (var p in new UIElement[] { ExplorePage, ChangesPage, UpdatesPage, SettingsPage }) p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed;
        currentPage = page == ExplorePage ? "Explore" : page == ChangesPage ? "Changes" : page == UpdatesPage ? "Updates" : "Settings";
        foreach (var name in new[] { nameof(IsExplore), nameof(IsChanges), nameof(IsUpdates), nameof(IsSettings) }) Changed(name);
    }
    void CardClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FeatureCard card }) return;
        if (card.Guide is CuratedEntry guide) SelectGuide(guide); else if (card.Feature is Feature feature) Selected = feature;
        if (compact) { detailOpen = true; UpdateLayoutMode(); }
    }
    void CopyIdClick(object sender, RoutedEventArgs e) => Safe(() => { if (sender is Button { Tag: uint id }) Clipboard.SetText(id.ToString(CultureInfo.InvariantCulture)); });
    void ExploreClick(object sender, RoutedEventArgs e) => ShowPage(ExplorePage);
    void ChangesClick(object sender, RoutedEventArgs e) => ShowPage(ChangesPage);
    void UpdatesClick(object sender, RoutedEventArgs e) => ShowPage(UpdatesPage);
    void SettingsClick(object sender, RoutedEventArgs e) => ShowPage(SettingsPage);
    async void ToggleClick(object sender, RoutedEventArgs e)
    {
        var target = ToggleState == true ? OverrideState.Disabled : OverrideState.Enabled;
        if (CanToggle) await ChangeSelectedAsync(new Snapshot(true, target));
        else RefreshDetail();
    }
    async void DefaultClick(object sender, RoutedEventArgs e)
    {
        if (CanRestoreDefault) await ChangeSelectedAsync(Snapshot.Default);
    }
    async Task<IReadOnlyList<ChangeResult>> ExecuteImmediateAsync(IReadOnlyList<Change> changes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        activeReceipt = new Receipt(Guid.NewGuid(), DateTimeOffset.Now, BuildText, changes.ToList());
        SaveReceipt(activeReceipt);
        try
        {
            var response = demo ? new WorkerResponse(ChangeEngine.Apply(store, changes), null) : await Program.ElevateAsync(changes.ToList(), L.Language);
            if (response.Error is not null) throw new InvalidOperationException(response.Error);
            return response.Results ?? throw new InvalidDataException("Missing worker result.");
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223) { throw new OperationCanceledException("UAC canceled.", error); }
    }
    async Task ChangeSelectedAsync(Snapshot target)
    {
        if (!SupportsOverrides || busy || refreshing || inspectingId || closed || Selected is null || selectedGuide is not null) return;
        var feature = Selected;
        if (CuratedCatalog.IsHistoricalReference(feature.Id)) { RefreshDetail(); return; }
        SetBusy(true); activeReceipt = null;
        // Restore the bound actual value immediately; the click is not an optimistic state update.
        Changed(nameof(ToggleState));
        try
        {
            var outcome = await toggle.ApplyAsync(feature, target);
            if (activeReceipt is not null)
                SaveReceipt(activeReceipt with { Results = new List<ChangeResult> { new(activeReceipt.Changes[0], outcome.Succeeded, outcome.Error?.Message ?? (outcome.Succeeded ? null : "No confirmed result.")) } });
            if (outcome.Error is not null) ReportError(outcome.Error);
            else { lastError = null; Changed(nameof(DiagnosticVisibility)); SetStatus(L[outcome.Change?.Before == target ? "Unchanged" : "Restart"]); }
        }
        catch (Exception error) { ReportError(error); }
        finally { activeReceipt = null; SetBusy(false); try { LoadHistory(); } catch (Exception error) { ReportError(error); } finally { if (!closed) RefreshDetail(); } }
    }
    void InspectClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        if (!uint.TryParse(Search, out var id) || id == 0) throw new InvalidDataException(L["InvalidId"]);
        searchTimer.Stop(); Filter();
        var card = Cards.FirstOrDefault(c => c.Feature?.Id == id || c.Guide?.FeatureIds.Contains(id) == true);
        if (card is null) return; // A numeric lookup never bypasses visibility filters.
        if (card.Guide is not null) SelectGuide(card.Guide); else Selected = card.Feature;
        if (compact) { detailOpen = true; UpdateLayoutMode(); }
    });
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
        string[] files;
        try { files = Directory.EnumerateFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(100).ToArray(); }
        catch (Exception error) { ReportError(error); return; }
        foreach (var file in files)
        {
            try { var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(file)); if (receipt is not null) History.Add(new(receipt, L)); }
            catch (Exception e) { ReportError(new IOException(Path.GetFileName(file) + ": " + e.Message, e)); }
        }
    }
    void ReportError(Exception error)
    {
        lastError = error; SetStatus(L.ErrorSummary(error)); Changed(nameof(DiagnosticVisibility));
    }
    void DiagnosticClick(object sender, RoutedEventArgs e)
    {
        if (lastError is not null) LocalizedDialog.Show(this, L, L["Error"], L.ErrorSummary(lastError), technical: lastError.ToString());
    }
    void Safe(Action action) { try { action(); } catch (Exception e) { ReportError(e); } }
    void ReleaseClick(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/BlakeLiAFK/ViVeUI/releases/latest");
    void LicenseClick(object sender, RoutedEventArgs e) => Safe(() =>
    {
        using var input = typeof(Program).Assembly.GetManifestResourceStream("ViVeUI.LICENSE")!;
        using var reader = new StreamReader(input);
        var viewer = new TextBox { Text = reader.ReadToEnd(), IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16) };
        new Window { Icon = Icon, Title = "ViVeUI · GPL-3.0-or-later", Width = 720, Height = 600, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = viewer }.ShowDialog();
    });
    void SourceClick(object sender, RoutedEventArgs e) => OpenUrl(Catalog.Source);
    void HistoricalClick(object sender, RoutedEventArgs e)
    {
        var source = Selected is null ? null : CuratedCatalog.All
            .FirstOrDefault(entry => entry.Kind == CuratedKind.Historical && entry.FeatureIds.Contains(Selected.Id))?.Sources.FirstOrDefault();
        if (source is not null && CuratedCatalog.IsAllowedSource(source) && !demo) OpenUrl(source);
    }
    void OpenUrl(string url) => Safe(() => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }));
    async void RefreshClick(object sender, RoutedEventArgs e) => await RefreshObservations();
    async void CheckClick(object sender, RoutedEventArgs e) => await CheckUpdates();
    async Task CheckUpdates()
    {
        if (closed || updateBusy) return; BeginUpdate(); UpdateDetails = ""; Changed(nameof(UpdateDetails));
        try
        {
            var pending = updater.CheckAsync(BuildInfo.Version, RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64", updateCancellation!.Token); Changed(nameof(UpdateText));
            release = await pending; UpdateDetails = release is null ? "" : $"{release.Version} · {release.Asset.Size / 1024 / 1024} MB\n{release.Page}";
        }
        catch (OperationCanceledException) { release = null; UpdateDetails = L["Canceled"]; }
        catch (Exception e) { release = null; ReportError(e); UpdateDetails = L.ErrorSummary(e); }
        finally { EndUpdate(); Changed(nameof(UpdateText)); Changed(nameof(UpdateDetails)); }
        if (!closed && release is not null && AutoDownload) await Download();
    }
    async void DownloadClick(object sender, RoutedEventArgs e) => await Download();
    async Task Download()
    {
        if (closed || release is null || updateBusy) return; BeginUpdate(); DownloadProgress = 0; Changed(nameof(DownloadProgress));
        try
        {
            var pending = updater.DownloadAsync(release.Asset, Path.Combine(folder, "downloads"), new Progress<double>(p => { DownloadProgress = p * 100; Changed(nameof(DownloadProgress)); }), updateCancellation!.Token); Changed(nameof(UpdateText));
            var path = await pending; UpdateDetails = path + "\nSHA-256: " + release.Asset.Sha256;
        }
        catch (OperationCanceledException) { DownloadProgress = 0; Changed(nameof(DownloadProgress)); UpdateDetails = L["Canceled"]; }
        catch (Exception e) { ReportError(e); UpdateDetails = L.ErrorSummary(e); }
        finally { EndUpdate(); Changed(nameof(UpdateText)); Changed(nameof(UpdateDetails)); }
    }
    void OpenDownloadsClick(object sender, RoutedEventArgs e) => Safe(() => { var path = Path.Combine(folder, "downloads"); Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); });
    void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox?.SelectedItem is not LanguageChoice item) return;
        var previousGuide = selectedGuide;
        var previousStatus = statusKey;
        LanguagePreference = item.Code; L.Set(item.Code); Changed(nameof(LanguagePreference));
        foreach (var language in Languages) language.Refresh();
        Changed(nameof(TransitionArrow));
        var selectedId = Selected?.Id;
        discovered = discovered.Select(f => f with { Name = L["UnknownId"] }).ToArray();
        Filter();
        if (selectedId is uint id) Selected = catalog.FirstOrDefault(f => f.Id == id) ?? discovered.FirstOrDefault(f => f.Id == id) ?? new Feature(id, L["UnknownId"]);
        SelectGuide(previousGuide);
        foreach(var result in ExecutionResults.ToArray()) { var index = ExecutionResults.IndexOf(result); ExecutionResults[index] = result with { Locale = L }; }
        foreach (var name in new[] { nameof(Cards), nameof(BuildText), nameof(ModeText), nameof(UpdateText), nameof(CountText), nameof(ExecutionNotice) }) Changed(name);
        RefreshDetail(); LoadHistory(); SaveSettings();
        if (previousStatus is not null) SetStatus(L[previousStatus]);
        else if (lastError is not null) SetStatus(L.ErrorSummary(lastError));
    }
    void ScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Root is not null) { var scale = ViewScale.Normalize(e.NewValue); Root.LayoutTransform = new ScaleTransform(scale,scale); if (initialized) { UpdateLayoutMode(); SaveSettings(); } } }
    void SaveSettings()
    {
        if (!initialized) return;
        Safe(() => { Directory.CreateDirectory(folder); AtomicWrite(Path.Combine(folder, "settings.json"), JsonSerializer.Serialize(new Preferences(LanguagePreference, AutoCheck, AutoDownload, ThemeBox.SelectedIndex, ViewScale.Normalize(ScaleSlider.Value)))); });
    }
    void LoadSettings()
    {
        try
        {
            var path = Path.Combine(folder, "settings.json"); if (!File.Exists(path)) return;
            var prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)); if (prefs is null) return;
            autoCheck = prefs.AutoCheck; autoDownload = prefs.AutoDownload; ThemeBox.SelectedIndex = Math.Clamp(prefs.Theme, 0, 2);
            ScaleSlider.Value = ViewScale.Normalize(prefs.Scale);
            LanguagePreference = Localization.NormalizeSelection(prefs.Language);
            LanguageBox.SelectedValue = LanguagePreference; Changed(nameof(LanguagePreference));
        }
        catch (Exception e) { ReportError(e); }
    }
    public async Task SmokeAsync()
    {
        if (!demo || store is not DemoStore) throw new InvalidOperationException("Smoke tests require fake backend.");
        // Fixed WPF render viewports avoid depending on the hosted runner display size.
        File.WriteAllText("icon-result.json", JsonSerializer.Serialize(IconResources.Validate(this)));
        Root.Width = 1440; Root.Height = 900; UpdateLayoutMode();
        ThemeBox.SelectedIndex = 2;
        if (((SolidColorBrush)FindResource("CanvasBrush")).Color != (Color)ColorConverter.ConvertFromString("#151D2B")) throw new Exception("Dark appearance failed.");
        ThemeBox.SelectedIndex = 1;
        SidebarLanguage.SelectedValue = "en";
        if (LanguageBox.SelectedValue as string != "en") throw new Exception("Sidebar language synchronization failed.");
        Search = "37634385"; Filter(); if (Filtered.Count != 1) throw new Exception("Search failed.");
        discovered = [new Feature(4294967201, "Demo smoke one")];
        ShowUnknown = true; Search = ""; searchTimer.Stop(); Filter();
        Selected = discovered[0];
        if (ToggleState is not null) throw new Exception("Default must be indeterminate.");
        await ChangeSelectedAsync(new Snapshot(true, OverrideState.Enabled));
        if (ToggleState != true) throw new Exception("Immediate enable failed.");
        Search = ""; searchTimer.Stop(); ShowUnknown = true;
        ShowPage(ExplorePage); await Capture("explore.png");
        ShowPage(ChangesPage); await Capture("history.png");
        LanguageBox.SelectedValue = "zh-Hans"; Selected = new Feature(4294967201, "Demo smoke one");
        ShowPage(ExplorePage); await Capture("explore-zh.png");
        if (ToggleState != true) throw new Exception("Language switch lost actual state.");
        await ChangeSelectedAsync(new Snapshot(true, OverrideState.Disabled));
        if (ToggleState != false) throw new Exception("Immediate disable failed.");
        await ChangeSelectedAsync(Snapshot.Default);
        if (ToggleState is not null || store.Read(4294967201) != Snapshot.Default) throw new Exception("Restore default failed.");
        ShowPage(ChangesPage); await Capture("history-zh.png");
        Root.Width = 900; UpdateLayoutMode(); await Capture("compact-history.png");
        Root.Width = 1440; UpdateLayoutMode();
        LanguageBox.SelectedValue = "es"; ShowPage(SettingsPage); await Capture("settings-es.png");
        ShowPage(UpdatesPage); await Capture("updates.png");
        LanguageBox.SelectedValue = "zh-Hans"; ShowPage(ExplorePage); ShowUnknown = true; await Capture("unified-unknown-zh.png");
        Root.Width = 900; ShowUnknown = false; UpdateLayoutMode(); await Capture("compact-catalog.png");
        OpenDetailClick(this, new()); await Capture("compact-detail.png");
        Search = "4294967295"; InspectClick(this, new());
        if (Selected?.Id == uint.MaxValue) throw new Exception("Numeric lookup bypassed unknown-item visibility.");
        File.WriteAllText("smoke-result.txt", "PASS: WPF startup, all-ID search, curated cards, immediate checkbox enable/disable/default, fake readback, history, localized selection retention, 10 native renders. No feature settings modified.");
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    void AssertViewportVisible(FrameworkElement element)
    {
        if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0) throw new Exception("Required control is not visible.");
        var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        for (DependencyObject? parent = element; parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is UIElement visual && visual.Visibility != Visibility.Visible) throw new Exception("Required control has a hidden ancestor.");
            if (parent is FrameworkElement viewport && (parent == Root || parent is ScrollContentPresenter))
            {
                var visible = element.TransformToAncestor(viewport).TransformBounds(bounds);
                if (visible.Left < -1 || visible.Top < -1 || visible.Right > viewport.ActualWidth + 1 || visible.Bottom > viewport.ActualHeight + 1)
                    throw new Exception($"Required control {element.Name} is clipped by {viewport.GetType().Name}: {visible} in {viewport.ActualWidth}x{viewport.ActualHeight}.");
            }
            if (parent == Root) break;
        }
    }
    async Task Capture(string filename, double dpiScale = 1)
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
        Directory.CreateDirectory("previews");
        // Detach for an offscreen layout: a hosted desktop may be only 1024px wide.
        // Rendering a still-parented root would inherit its window's clip.
        Content = null;
        UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (VisualTreeHelper.GetParent(Root) is not null) throw new InvalidOperationException("Preview root did not detach from its desktop window.");
        var host = new Border { Width = Root.Width, Height = Root.Height, Child = Root, DataContext = this };
        // Keep per-window theme overrides when rendering outside the native window.
        foreach (var key in Resources.Keys) host.Resources[key] = Resources[key];
        try
        {
            Root.DataContext = this;
            // Reparenting the Window content can materialize inherited direction as a local value.
            // Restore its live locale binding for the detached render and the returned UI tree.
            Root.SetBinding(FlowDirectionProperty, new System.Windows.Data.Binding(nameof(Locale.Direction)) { Source = L });
            System.Windows.Documents.TextElement.SetFontFamily(Root, FontFamily);
            System.Windows.Documents.TextElement.SetFontSize(Root, FontSize);
            System.Windows.Documents.TextElement.SetForeground(Root, Foreground);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var size = new Size(Root.Width, Root.Height);
            Root.InvalidateMeasure(); Root.InvalidateArrange();
            host.Measure(size); host.Arrange(new Rect(size)); host.UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            host.UpdateLayout();
            var activeNav = IsExplore ? ExploreNavigation : IsChanges ? ChangesNavigation : IsUpdates ? UpdatesNavigation : SettingsNavigation;
            if (activeNav.Background is not SolidColorBrush navBrush || navBrush.Color != ((SolidColorBrush)FindResource("SelectionBrush")).Color) throw new Exception("Active navigation highlight missing.");
            if (Math.Abs(Root.ActualWidth - size.Width) > 1 || Math.Abs(Root.ActualHeight - size.Height) > 1) throw new InvalidOperationException("Preview viewport was clipped.");
            if (IsExplore && size.Width == 1440 && size.Height == 900 && (L.Language is "en" or "zh-Hans"))
            {
                var cards = Descendants(GalleryItems).OfType<Button>().Where(b => b.DataContext is FeatureCard && b.Tag is FeatureCard).ToArray();
                if (cards.Length != Cards.Count()) throw new Exception("Curated catalog rendering lost entries.");
                foreach (var card in cards.Take(2)) AssertViewportVisible(card);
                AssertViewportVisible(selectedGuide is null ? EnableCheckBox : GuideAction);
            }
            if (filename.StartsWith("localization/", StringComparison.Ordinal))
            {
                AssertViewportVisible(ContextHeader);
                if (IsExplore && DetailPane.Visibility == Visibility.Visible)
                    foreach (var control in new FrameworkElement[] { EnableCheckBox }) AssertViewportVisible(control);
                if (IsExplore && GuideDetailPane.Visibility == Visibility.Visible) AssertViewportVisible(GuideAction);
                if (IsChanges) AssertViewportVisible(HistoryList);
            }
            var bitmap = new RenderTargetBitmap((int)(size.Width * dpiScale), (int)(size.Height * dpiScale), 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
            bitmap.Render(host);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var output = Path.Combine("previews", filename); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using var stream = File.Create(output); encoder.Save(stream);
        }
        finally
        {
            host.Child = null; Content = Root;
            Root.SetBinding(FlowDirectionProperty, new System.Windows.Data.Binding(nameof(Locale.Direction)) { Source = L });
        }

    }
}
public sealed record Preferences(string Language, bool AutoCheck, bool AutoDownload, int Theme = 0, double Scale = 1);
public sealed class HistoryRow(Receipt receipt, Locale locale)
{
    public Receipt Receipt { get; } = receipt;
    public string Summary => $"{Receipt.At:g} · {locale.Format("QueueCountFormat", Receipt.Changes.Count)} · {locale[Receipt.Results is null ? "Pending" : Receipt.Results.Count == Receipt.Changes.Count && Receipt.Results.All(r => r.Applied) ? "Applied" : "Partial"]}";
    public string Detail => string.Join("\n", Receipt.Changes.Select(c => $"{Localization.FeatureId(c.Id)}: {locale.State(c.Before)} → {locale.State(c.After)}")) + (Receipt.Results is null ? "" : "\n" + string.Join("\n", Receipt.Results.Where(r => r.Error is not null).Select(r => locale.ErrorSummary(new Exception(r.Error)))));
}

public sealed class Choice<T>(T key, Locale locale, string textKey) : INotifyPropertyChanged
{
    public T Key { get; } = key;
    public string Value => locale[textKey];
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new(nameof(Value)));
}
