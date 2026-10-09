using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public partial class MainWindow
{
    public async Task UxSmokeAsync()
    {
        if(!demo || store is not DemoStore fake)throw new Exception("UX validation requires fake feature storage.");
        Root.Width=1440;Root.Height=900;UpdateLayoutMode();ThemeBox.SelectedIndex=1;LanguageBox.SelectedValue="en";
        var icon=IconResources.Validate(this);
        if(Cards.Count()>CatalogPageSize || CuratedCatalog.All.Count<200)throw new Exception("Expanded curated catalog is incomplete.");
        SelectGuide(CuratedCatalog.All[0]);ShowPage(ExplorePage);await Capture("ux/classic-menu.png");
        var guideStates=catalog.ToDictionary(feature=>feature.Id,feature=>fake.Read(feature.Id));
        GuideOpenClick(this,new());
        if(LastGuideDestination!="explorer.exe" || CanToggle || CanRestoreDefault || guideStates.Any(pair=>fake.Read(pair.Key)!=pair.Value))throw new Exception("Guide crossed into immediate mutation scope.");
        LanguageBox.SelectedValue="zh-Hans";Search="右键";searchTimer.Stop();Filter();
        if(Cards.Count()<3 || selectedGuide?.Id!="ClassicMenu")throw new Exception("Localized guide search or language preservation failed.");
        await Capture("ux/right-click-search-zh.png");
        Search="no matching item 012398765";searchTimer.Stop();Filter();if(Cards.Any() || GalleryEmptyVisibility!=Visibility.Visible)throw new Exception("Empty search state missing.");await Capture("ux/empty-search.png");
        ClearSearchClick(this,new());searchTimer.Stop();if(Cards.Count()!=CatalogPageSize)throw new Exception("Clear search did not restore curated results.");
        var dropdowns=new List<object>();
        foreach(var language in new[]{"en","zh-Hans","de","ar"})
        {
            LanguageBox.SelectedValue=language;ShowPage(SettingsPage);ThemeBox.SelectedIndex=1;
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var peer=new ComboBoxAutomationPeer(LanguageBox);var expand=(IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse)!;
            expand.Expand();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var popup=(Popup)LanguageBox.Template.FindName("PART_Popup",LanguageBox);
            if(!popup.IsOpen || popup.Child is not FrameworkElement popupContent || popupContent.FlowDirection!=L.Direction)throw new Exception($"Popup state or RTL direction incorrect: {language}, combo={LanguageBox.IsDropDownOpen}, popup={popup.IsOpen}, child={(popup.Child as FrameworkElement)?.FlowDirection}, expected={L.Direction}.");
            var surface=(Border)LanguageBox.Template.FindName("PopupSurface",LanguageBox);
            if(surface.Background is not SolidColorBrush paper || paper.Color!=((SolidColorBrush)FindResource("PaperBrush")).Color)throw new Exception("Popup did not inherit the active palette.");
            var scroller=Descendants(popupContent).OfType<ScrollViewer>().First();
            if(scroller.ScrollableHeight<=0)throw new Exception("Language popup must scroll through all seventeen choices.");
            scroller.ScrollToEnd();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            if(scroller.VerticalOffset<=0)throw new Exception("Dropdown scrolling failed.");
            await RenderPopup(popupContent,$"ux/dropdown-{language}.png");
            expand.Collapse();if(LanguageBox.IsDropDownOpen)throw new Exception("Automation collapse failed.");
            dropdowns.Add(new {language,expanded=true,collapsed=true,scrolled=true,rtl=language=="ar",items=LanguageBox.Items.Count});
        }
        LanguageBox.SelectedValue="en";ShowPage(SettingsPage);Keyboard.Focus(ThemeBox);
        void Key(ComboBox combo,System.Windows.Input.Key key)
        {
            var source=PresentationSource.FromVisual(combo) ?? throw new Exception("Keyboard test has no native presentation source.");
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,key){RoutedEvent=Keyboard.KeyDownEvent});
        }
        Key(ThemeBox,System.Windows.Input.Key.F4);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(!ThemeBox.IsDropDownOpen)throw new Exception("F4 did not open the dropdown.");
        Key(ThemeBox,System.Windows.Input.Key.End);Key(ThemeBox,System.Windows.Input.Key.Enter);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(ThemeBox.IsDropDownOpen || ThemeBox.SelectedIndex!=2)throw new Exception("End/Enter dropdown selection failed.");
        Key(ThemeBox,System.Windows.Input.Key.F4);Key(ThemeBox,System.Windows.Input.Key.Escape);
        if(ThemeBox.IsDropDownOpen)throw new Exception("Escape did not close the dropdown.");
        LanguageBox.IsDropDownOpen=true;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var darkPopup=(Popup)LanguageBox.Template.FindName("PART_Popup",LanguageBox);
        var darkSurface=(Border)LanguageBox.Template.FindName("PopupSurface",LanguageBox);
        if(((SolidColorBrush)darkSurface.Background).Color!=((SolidColorBrush)FindResource("PaperBrush")).Color)throw new Exception("Dark popup palette mismatch.");
        await RenderPopup((FrameworkElement)darkPopup.Child,"ux/dropdown-dark.png");LanguageBox.IsDropDownOpen=false;
        LanguageBox.IsEnabled=false;await Capture("ux/settings-disabled-dark.png");
        if(LanguageBox.IsEnabled)throw new Exception("Disabled dropdown state failed.");LanguageBox.IsEnabled=true;ThemeBox.SelectedIndex=1;
        ScaleSlider.Value=1.25;SaveSettings();
        var saved=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(folder,"settings.json")))!;
        if(saved.Scale!=1.25)throw new Exception("Scale was not persisted.");
        var reopened=new MainWindow(true,folder);if(reopened.ScaleSlider.Value!=1.25)throw new Exception("Scale did not survive window recreation.");reopened.Close();
        Root.LayoutTransform=Transform.Identity;Root.Width=1152;Root.Height=720;UpdateLayoutMode();await Capture("ux/settings-scale-125.png",1.25);ScaleSlider.Value=1;Root.Width=1440;Root.Height=900;UpdateLayoutMode();
        Search="";searchTimer.Stop();ShowKnown=true;ShowHistorical=true;ShowUnknown=true;ShowPage(ExplorePage);
        const uint immediateId=4294967201, failingId=4294967202, unrelatedId=4294967203;
        discovered=[new Feature(immediateId,"Demo immediate checkbox"),new Feature(failingId,"Demo immediate failure"),new Feature(unrelatedId,"Demo unrelated feature")];Filter();
        var enabled=new Snapshot(true,OverrideState.Enabled);var disabled=new Snapshot(true,OverrideState.Disabled);
        fake.Write(unrelatedId,disabled);
        async Task ToggleThroughControl()
        {
            var provider=(IToggleProvider)new CheckBoxAutomationPeer(EnableCheckBox).GetPattern(PatternInterface.Toggle)!;
            provider.Toggle();
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            if(EnableCheckBox.GetBindingExpression(ToggleButton.IsCheckedProperty) is null)throw new Exception("Toggling the checkbox removed its actual-state binding.");
        }
        Selected=new Feature(immediateId,"Demo immediate checkbox");
        if(ToggleState is not null || !CanToggle)throw new Exception("A default feature must expose an indeterminate editable checkbox.");
        SetBusy(true);
        try
        {
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            if(CanToggle || CanRestoreDefault || EnableCheckBox.IsEnabled)throw new Exception("Busy state left immediate controls enabled.");
            var busyClose=new System.ComponentModel.CancelEventArgs();ConfirmClose(this,busyClose);
            if(!busyClose.Cancel)throw new Exception("Closing was allowed while an immediate change was busy.");
            await ChangeSelectedAsync(enabled);
            if(fake.Read(immediateId)!=Snapshot.Default || fake.Read(unrelatedId)!=disabled)throw new Exception("Busy reentry modified fake storage.");
        }
        finally { SetBusy(false);RefreshDetail(); }
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var idleClose=new System.ComponentModel.CancelEventArgs();ConfirmClose(this,idleClose);
        if(idleClose.Cancel)throw new Exception("Idle window was blocked by a removed review-queue close guard.");
        await ToggleThroughControl();
        if(fake.Read(immediateId)!=enabled || ToggleState!=true || EnableCheckBox.IsChecked!=true || !CanRestoreDefault)throw new Exception("Immediate enable did not update fake storage and checkbox.");
        await Capture("ux/checkbox-enabled.png");
        await ToggleThroughControl();
        if(fake.Read(immediateId)!=disabled || ToggleState!=false || EnableCheckBox.IsChecked!=false)throw new Exception("Immediate disable did not update fake storage and checkbox.");
        await Capture("ux/checkbox-disabled.png");
        var defaultButton=Descendants(DetailPane).OfType<Button>().Single(button=>System.Windows.Automation.AutomationProperties.GetAutomationId(button)=="RestoreFeatureDefault");
        ((IInvokeProvider)new ButtonAutomationPeer(defaultButton).GetPattern(PatternInterface.Invoke)!).Invoke();
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(fake.Read(immediateId)!=Snapshot.Default || ToggleState is not null || EnableCheckBox.IsChecked is not null)throw new Exception("Restore-default did not restore the indeterminate checkbox.");
        await Capture("ux/checkbox-default.png");
        Keyboard.Focus(EnableCheckBox);
        var checkboxSource=PresentationSource.FromVisual(EnableCheckBox) ?? throw new Exception("Checkbox keyboard test has no presentation source.");
        foreach(var routedEvent in new[]{Keyboard.KeyDownEvent,Keyboard.KeyUpEvent})
            EnableCheckBox.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,checkboxSource,Environment.TickCount,System.Windows.Input.Key.Space){RoutedEvent=routedEvent});
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(fake.Read(immediateId)!=enabled || ToggleState!=true || EnableCheckBox.GetBindingExpression(ToggleButton.IsCheckedProperty) is null)throw new Exception("Space did not use the verified immediate-toggle path.");
        await ChangeSelectedAsync(Snapshot.Default);
        Selected=new Feature(failingId,"Demo immediate failure");fake.FailOn=failingId;lastError=null;
        await ChangeSelectedAsync(enabled);await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(lastError is null || fake.Read(failingId)!=Snapshot.Default || ToggleState is not null || fake.Read(unrelatedId)!=disabled)throw new Exception("Failed immediate change did not preserve observed and unrelated state.");
        await Capture("ux/checkbox-failed.png");fake.FailOn=0;
        await ChangeSelectedAsync(enabled);
        if(fake.Read(failingId)!=enabled || ToggleState!=true || fake.Read(unrelatedId)!=disabled)throw new Exception("Immediate retry failed or changed an unrelated feature.");
        if(!History.Any(row=>row.Receipt.Changes.Any(change=>change.Id==immediateId)))throw new Exception("Immediate changes did not produce history.");
        ShowPage(ChangesPage);await Capture("ux/immediate-history.png");
        // Manual inspection is an explicit read path, independent from catalog visibility.
        ShowKnown=true;ShowHistorical=true;ShowUnknown=false;
        Search=uint.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);searchTimer.Stop();Filter();
        var dictionaryBefore=catalog.Select(feature=>feature.Id).ToArray();
        var discoveryBefore=discovered.Select(feature=>feature.Id).ToArray();
        var manualStates=catalog.Concat(discovered).Select(feature=>feature.Id).Append(uint.MaxValue).Concat(CuratedCatalog.All.SelectMany(entry=>entry.FeatureIds)).Distinct().ToDictionary(id=>id,id=>fake.Read(id));
        var historyBeforeManual=History.Count;
        bool ManualStorageUnchanged()=>manualStates.All(pair=>fake.Read(pair.Key)==pair.Value);
        if(catalog.Any(feature=>feature.Id==uint.MaxValue) || catalogMatches.Any(item=>item.Feature?.Id==uint.MaxValue))throw new Exception("Unlisted manual-ID fixture unexpectedly exists in the searchable dictionary.");
        ManualIdText=" 4294967295 ";await InspectManualIdAsync();
        if(Selected?.Id!=uint.MaxValue || ToggleState is not null || ShowUnknown || IsFeatureVisible(uint.MaxValue) || SelectedProvenanceText!=L["InputIdNotCatalog"] || SelectedAvailabilityText!=L["InputIdNotObserved"])throw new Exception("Explicit unlisted-ID inspection confused provenance, observation or search visibility.");
        await Capture("ux/manual-unlisted-id.png");
        foreach(var invalid in new[]{"", "0", "-1", "+1", "1.0", "4294967296", "not-an-id", "١٢٣"})
        {
            var previousSelection=Selected;lastError=null;ManualIdText=invalid;await InspectManualIdAsync();
            if(lastError is not FormatException || Selected!=previousSelection || !ManualStorageUnchanged())throw new Exception("Invalid manual ID changed selection or storage: " + invalid);
        }
        ManualIdText=immediateId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SetBusy(true);
        try
        {
            var previousSelection=Selected;await InspectManualIdAsync();
            if(Selected!=previousSelection || !ManualStorageUnchanged())throw new Exception("Manual inspection bypassed an active mutation guard.");
        }
        finally{SetBusy(false);}
        var manualExpander=EnumerateLogicalChildren(Root).OfType<Expander>().Single(expander=>EnumerateLogicalChildren(expander).Contains(ManualIdInput));
        manualExpander.IsExpanded=true;await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var knownProbe=catalog.First(feature=>!CuratedCatalog.IsHistoricalReference(feature.Id));
        ManualIdInput.Text=knownProbe.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ManualIdInput.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Keyboard.Focus(ManualIdInput);
        var manualSource=PresentationSource.FromVisual(ManualIdInput) ?? throw new Exception("Manual-ID keyboard input has no native presentation source.");
        var enter=new KeyEventArgs(Keyboard.PrimaryDevice,manualSource,Environment.TickCount,System.Windows.Input.Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent};
        ManualIdInput.RaiseEvent(enter);
        var inspectionWait=System.Diagnostics.Stopwatch.StartNew();
        while(inspectingId && inspectionWait.Elapsed<TimeSpan.FromSeconds(10))await Task.Delay(10);
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(inspectingId || !enter.Handled || Selected?.Id!=knownProbe.Id || Selected.Name!=knownProbe.Name || SelectedProvenanceText!=L["InputIdKnown"] || ShowUnknown)throw new Exception("Enter-key manual inspection lost the known technical name or changed filters.");
        await Capture("ux/manual-known-id.png");
        var historicalProbe=CuratedCatalog.All.Where(entry=>entry.Kind==CuratedKind.Historical).SelectMany(entry=>entry.FeatureIds).First();
        ManualIdText=historicalProbe.ToString(System.Globalization.CultureInfo.InvariantCulture);await InspectManualIdAsync();
        if(Selected?.Id!=historicalProbe || CanToggle || CanRestoreDefault)throw new Exception("Manual historical-ID inspection exposed mutation controls.");
        await ChangeSelectedAsync(enabled);
        if(!ManualStorageUnchanged() || History.Count!=historyBeforeManual || !catalog.Select(feature=>feature.Id).SequenceEqual(dictionaryBefore) || !discovered.Select(feature=>feature.Id).SequenceEqual(discoveryBefore) || ShowUnknown)throw new Exception("Manual inspection mutated storage, history, dictionary or filter state.");
        await Capture("ux/manual-historical-id.png");
        manualExpander.IsExpanded=false;Search="";searchTimer.Stop();Filter();
        // Slow HTTP runs only in an injected handler. Immediate feature controls remain available.
        var slowFolder=Path.Combine(folder,"slow-update");var slow=new MainWindow(true,slowFolder,new UpdateService(new PausedHandler()));
        slow.release=new(new Version(99,0),new Uri("https://github.com/BlakeLiAFK/ViVeUI/releases/tag/v99.0"),new("ViVeUI-win-x64.exe",new Uri("https://github.com/BlakeLiAFK/ViVeUI/releases/download/v99.0/ViVeUI-win-x64.exe"),new string('0',64),4));
        var download=slow.Download();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        slow.discovered=[new Feature(4294967201,"Demo UX slow download")];slow.ShowUnknown=true;slow.Filter();slow.Selected=slow.discovered[0];
        if(!slow.UpdateBusy || !slow.NotBusy || !slow.CanToggle)throw new Exception("Download blocked the independent checkbox controls.");
        await slow.ChangeSelectedAsync(enabled);
        if(slow.store.Read(4294967201)!=enabled || slow.ToggleState!=true)throw new Exception("Immediate fake change failed during download.");
        slow.CancelUpdateClick(this,new());await download;
        if(slow.updater.Status!=UpdateStatus.Canceled || slow.UpdateBusy || Directory.GetFiles(slowFolder,"*.partial",SearchOption.AllDirectories).Any())throw new Exception("Canceled download left a ready or partial package.");slow.Close();
        File.WriteAllText("ux-result.json",JsonSerializer.Serialize(new {passed=true,icon,dropdowns,keyboardF4EndEnterEscape=true,disabled=true,scaleReopened=1.25,immediateEnableDisableDefault=true,checkboxAutomationToggle=true,checkboxSpaceKey=true,busyReentryBlocked=true,closeBlockedOnlyWhileBusy=true,defaultButtonAutomationInvoke=true,failedWritePreservesState=true,unrelatedFeaturePreserved=true,immediateHistory=true,manualUnlistedReadOnly=true,manualInvalidPreservesSelection=true,manualKeyboardEnter=true,manualBusyGuard=true,manualHistoricalReadOnly=true,manualDoesNotChangeCatalogOrFilters=true,updateCancellation=true,checkboxUsableDuringDownload=true,guideWrites=false,noNativeSettingsModified=true,manualNarratorAndPointerReviewRequired=true},new JsonSerializerOptions{WriteIndented=true}));
    }
    static async Task RenderPopup(FrameworkElement element,string filename)
    {
        await element.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);element.UpdateLayout();
        var width=(int)Math.Ceiling(element.ActualWidth);var height=(int)Math.Ceiling(element.ActualHeight);
        if(width<=0 || height<=0)throw new Exception("Dropdown popup has no renderable size.");
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(element);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path=Path.Combine("previews",filename);Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var stream=File.Create(path);encoder.Save(stream);
    }
    sealed class PausedHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {await Task.Delay(Timeout.Infinite,token);return new(HttpStatusCode.OK);}
    }
}
