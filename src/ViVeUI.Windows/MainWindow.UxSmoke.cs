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
        if(Cards.Count()!=20 || GuideCatalog.All.Count!=15)throw new Exception("Expanded curated catalog is incomplete.");
        SelectGuide(GuideCatalog.All[0]);ShowPage(ExplorePage);await Capture("ux/classic-menu.png");
        GuideOpenClick(this,new());if(LastGuideDestination!="explorer.exe" || Staged.Count!=0)throw new Exception("Guide crossed into mutation scope.");
        LanguageBox.SelectedValue="zh-Hans";Search="右键";searchTimer.Stop();Filter();
        if(Cards.Count()<3 || selectedGuide?.Key!="ClassicMenu")throw new Exception("Localized guide search or language preservation failed.");
        await Capture("ux/right-click-search-zh.png");
        Search="no matching item 012398765";searchTimer.Stop();Filter();if(Cards.Any() || GalleryEmptyVisibility!=Visibility.Visible)throw new Exception("Empty search state missing.");await Capture("ux/empty-search.png");
        ClearSearchClick(this,new());searchTimer.Stop();if(Cards.Count()!=20)throw new Exception("Clear search did not restore curated results.");
        var dropdowns=new List<object>();
        foreach(var language in new[]{"en","zh-Hans","de","ar"})
        {
            LanguageBox.SelectedValue=language;ShowPage(SettingsPage);ThemeBox.SelectedIndex=1;
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
        Staged.Clear();ExecutionResults.Clear();Search="";searchTimer.Stop();ShowAllIds=false;
        foreach(var id in new uint[]{37634385,39420424,34300186}){Selected=catalog.Single(f=>f.Id==id);Desired=OverrideState.Enabled;Stage();}
        fake.FailOn=39420424;await ApplyStaged();
        if(!Staged.Select(c=>c.Id).SequenceEqual(new uint[]{39420424,34300186}) || !ExecutionResults.Select(r=>r.StatusKey).SequenceEqual(new[]{"Applied","Failed","NotRun"}))throw new Exception("Partial failure lost retry scope or per-item outcomes.");
        ShowPage(ChangesPage);await Capture("ux/partial-results.png");fake.FailOn=0;
        await ApplyStaged();if(Staged.Count!=0)throw new Exception("Reviewed retry did not finish remaining items.");
        Staged.Clear();Selected=catalog.Single(f=>f.Id==36354489);Desired=OverrideState.Enabled;Stage();
        HistoryList.SelectedItem=History.First(r=>r.Receipt.Results?.Any(x=>x.Applied)==true);UndoClick(this,new());
        if(!Staged.Any(c=>c.Id==36354489) || Staged.Count<2)throw new Exception("Restore discarded unrelated staged work.");
        // Slow HTTP runs only in an injected handler. Browsing and review edits remain available.
        var slowFolder=Path.Combine(folder,"slow-update");var slow=new MainWindow(true,slowFolder,new UpdateService(new PausedHandler()));
        slow.release=new(new Version(99,0),new Uri("https://github.com/BlakeLiAFK/ViVeUI/releases/tag/v99.0"),new("ViVeUI-win-x64.exe",new Uri("https://github.com/BlakeLiAFK/ViVeUI/releases/download/v99.0/ViVeUI-win-x64.exe"),new string('0',64),4));
        var download=slow.Download();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        slow.Selected=slow.catalog.Single(f=>f.Id==37634385);slow.Desired=OverrideState.Enabled;slow.Stage();slow.Acknowledged=true;
        if(!slow.UpdateBusy || !slow.NotBusy || !slow.CanApply || slow.Staged.Count!=1)throw new Exception("Download blocked the independent review workflow.");
        slow.CancelUpdateClick(this,new());await download;
        if(slow.updater.Status!=UpdateStatus.Canceled || slow.UpdateBusy || Directory.GetFiles(slowFolder,"*.partial",SearchOption.AllDirectories).Any())throw new Exception("Canceled download left a ready or partial package.");slow.Close();
        var closeDialog=LocalizedDialog.Create(this,L,L["Review"],L["ClosePending"],true);
        if(!EnumerateLogicalChildren((DependencyObject)closeDialog.Content).OfType<Button>().Any(b=>b.IsDefault && b.IsCancel))throw new Exception("Close guard must default to Cancel.");closeDialog.Close();
        File.WriteAllText("ux-result.json",JsonSerializer.Serialize(new {passed=true,icon,dropdowns,keyboardF4EndEnterEscape=true,disabled=true,scaleReopened=1.25,partialQueuePreserved=true,restoreMergePreserved=true,updateCancellation=true,reviewEditableDuringDownload=true,closeGuardDefaultsCancel=true,guideWrites=false,noFeatureWrites=true,manualNarratorAndPointerReviewRequired=true},new JsonSerializerOptions{WriteIndented=true}));
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
