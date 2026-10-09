using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ViVeUI.Core;
namespace ViVeUI.Windows;

public partial class MainWindow
{
    RecipeToggleController? recipeToggle;
    readonly List<RecipeCardModel> allRecipeCards = [];
    public IReadOnlyList<RecipeCardModel> AllRecipeCards => allRecipeCards;
    public string HeaderCatalogCount => IsRecipes ? L.Format("RecipeCountFormat",FeatureRecipes.All.Count) : L["CatalogCountChip"];
    public IEnumerable<RecipeCardModel> RecipeCards => MatchingRecipes().Where(RecipeVisible).Skip(recipePage * CatalogPageSize).Take(CatalogPageSize);
    int recipePage;
    string recipeSearch = "";
    bool showApplicable = true, showNotApplicable, showUnconfirmed = true;
    public string RecipeSearch { get => recipeSearch; set { recipeSearch=value ?? ""; recipePage=0; RefreshRecipeView(); } }
    public bool ShowApplicable { get=>showApplicable; set {showApplicable=value;recipePage=0;RefreshRecipeView();} }
    public bool ShowNotApplicable { get=>showNotApplicable; set {showNotApplicable=value;recipePage=0;RefreshRecipeView();} }
    public bool ShowUnconfirmed { get=>showUnconfirmed; set {showUnconfirmed=value;recipePage=0;RefreshRecipeView();} }
    public string RecipeApplicableText => RecipeFilterLabel(RecipeApplicability.Applicable);
    public string RecipeNotApplicableText => RecipeFilterLabel(RecipeApplicability.NotApplicable);
    public string RecipeUnconfirmedText => RecipeFilterLabel(RecipeApplicability.Unconfirmed);
    public string RecipeCountText => L.Format("RecipeCountFormat",MatchingRecipes().Count(RecipeVisible));
    public int RecipePageCount => Math.Max(1,(MatchingRecipes().Count(RecipeVisible)+CatalogPageSize-1)/CatalogPageSize);
    public string RecipePageText => L.Format("CatalogPageFormat",recipePage+1,RecipePageCount);
    public bool CanPreviousRecipePage => recipePage>0;
    public bool CanNextRecipePage => recipePage+1<RecipePageCount;
    public Visibility RecipeEmptyVisibility => RecipeCards.Any()?Visibility.Collapsed:Visibility.Visible;
    string RecipeFilterLabel(RecipeApplicability applicability) => $"{L["RecipeFilter"+applicability]} ({MatchingRecipes().Count(c=>c.Applicability==applicability).ToString(CultureInfo.CurrentCulture)})";
    bool RecipeVisible(RecipeCardModel card) => card.Applicability switch { RecipeApplicability.Applicable=>ShowApplicable, RecipeApplicability.NotApplicable=>ShowNotApplicable, _=>ShowUnconfirmed };
    IEnumerable<RecipeCardModel> MatchingRecipes() => allRecipeCards.Where(c=>string.IsNullOrWhiteSpace(recipeSearch) || (c.Title+" "+c.Purpose+" "+c.Ids).Contains(recipeSearch.Trim(),StringComparison.OrdinalIgnoreCase));
    void InitializeRecipes()
    {
        recipeToggle=new(store,ExecuteRecipeAsync);
        allRecipeCards.Clear();
        allRecipeCards.AddRange(FeatureRecipes.All.Select(r=>new RecipeCardModel(r,L)));
        RefreshRecipes();
    }
    void RefreshRecipes()
    {
        if(recipeToggle is null)return;
        var observed=observations.Keys.ToHashSet();
        foreach(var card in allRecipeCards)
        {
            var actual=recipeToggle.Read(card.Recipe.Id);
            card.Update(actual,FeatureRecipes.Evaluate(card.Recipe,CurrentDeviceBuild,observed),
                SupportsOverrides && !busy && !refreshing && !closed && observationError is null);
        }
        RefreshRecipeView();
    }
    void RefreshRecipeView()
    {
        recipePage=Math.Min(recipePage,RecipePageCount-1);
        foreach(var property in new[]{nameof(RecipeSearch),nameof(ShowApplicable),nameof(ShowNotApplicable),nameof(ShowUnconfirmed),nameof(RecipeCards),nameof(RecipeCountText),nameof(RecipeApplicableText),nameof(RecipeNotApplicableText),nameof(RecipeUnconfirmedText),nameof(RecipePageText),nameof(CanPreviousRecipePage),nameof(CanNextRecipePage),nameof(RecipeEmptyVisibility)})Changed(property);
    }
    async void RecipeRefreshClick(object sender,RoutedEventArgs e){await RefreshObservations();RefreshRecipes();}
    public string? LastCopiedRecipeIds {get;private set;}
    void RecipeCopyIdsClick(object sender,RoutedEventArgs e)=>Safe(()=>{if(sender is Button {Tag:RecipeCardModel card}){LastCopiedRecipeIds=card.Ids;if(!demo)Clipboard.SetText(card.Ids);}});
    void RecipeSourceClick(object sender,RoutedEventArgs e)=>Safe(()=>
    {
        if(sender is not Button {Tag:string source} || !allRecipeCards.Any(c=>c.Recipe.Sources.Contains(source)) || !CuratedCatalog.IsAllowedSource(source))return;
        if(demo)LastGuideDestination=source;else OpenUrl(source);
    });
    async void RecipeToggleClick(object sender,RoutedEventArgs e)
    {
        if(sender is ImmediateCheckBox {Tag:RecipeCardModel card} && card.CanToggle)
            await ChangeRecipeAsync(card,new Snapshot(true,card.State==true?OverrideState.Disabled:OverrideState.Enabled));
    }
    async void RecipeDefaultClick(object sender,RoutedEventArgs e)
    {
        if(sender is Button {Tag:RecipeCardModel card} && card.CanRestoreDefault)await ChangeRecipeAsync(card,Snapshot.Default);
    }
    async Task ChangeRecipeAsync(RecipeCardModel card,Snapshot target)
    {
        if(recipeToggle is null || !card.CanToggle || busy || closed)return;
        SetBusy(true);activeReceipt=null;card.SetResult("RecipeBusy",[]);RefreshRecipes();
        try
        {
            var outcome=await recipeToggle.ApplyAsync(card.Recipe.Id,target,CurrentDeviceBuild,observations.Keys.ToHashSet());
            var results=outcome.Changes.Select(change=>
            {
                var actual=outcome.Actual.FirstOrDefault(a=>a.Id==change.Id);
                var reported=outcome.Results.FirstOrDefault(r=>r.Change.Id==change.Id);
                var verified=actual?.ReadSucceeded==true && actual.Snapshot==change.After;
                return new ChangeResult(change,verified && (change.Before==change.After || reported?.Applied==true),actual?.Error?.Message ?? reported?.Error ?? (verified?null:outcome.Error?.Message));
            }).ToList();
            if(activeReceipt is not null)SaveReceipt(activeReceipt with {Results=results});
            card.SetResult(outcome.Succeeded?"RecipeResultSuccess":"RecipeResultFailed",results);
            if(outcome.Error is not null)ReportError(outcome.Error);
            else SetStatus(L["RecipeResultSuccess"]);
        }
        catch(Exception error){card.SetResult("RecipeResultFailed",[]);ReportError(error);}
        finally
        {
            activeReceipt=null;SetBusy(false);
            try{LoadHistory();}catch(Exception error){ReportError(error);}
            RefreshRecipes();
        }
    }
    void PreviousRecipePageClick(object sender,RoutedEventArgs e){if(CanPreviousRecipePage){recipePage--;RefreshRecipeView();RecipeScroll.ScrollToTop();}}
    void NextRecipePageClick(object sender,RoutedEventArgs e){if(CanNextRecipePage){recipePage++;RefreshRecipeView();RecipeScroll.ScrollToTop();}}
}

public sealed class RecipeCardModel(FeatureRecipe recipe,Locale locale):INotifyPropertyChanged
{
    public FeatureRecipe Recipe {get;}=recipe;
    public Locale L=>locale;
    public string Title=>CuratedCatalog.Text(CuratedCatalog.All.First(e=>e.Id==Recipe.Id),L.Language).Title;
    public string Purpose=>L["RecipePurpose"+Recipe.Id];
    public string Ids=>string.Join(", ",Recipe.FeatureIds.Select(id=>id.ToString(CultureInfo.InvariantCulture)));
    public RecipeApplicability Applicability {get;private set;}=RecipeApplicability.Unconfirmed;
    public string ApplicabilityText=>L["RecipeFilter"+Applicability];
    IReadOnlyList<ImmediateToggleState> actual=[];
    IReadOnlyList<ChangeResult> results=[];
    string? resultKey;
    public bool? State=>actual.Count==Recipe.FeatureIds.Count && actual.All(a=>a.ReadSucceeded && a.State==true)?true:actual.Count==Recipe.FeatureIds.Count && actual.All(a=>a.ReadSucceeded && a.State==false)?false:null;
    public string CurrentOverride=>actual.Count!=Recipe.FeatureIds.Count || actual.Any(a=>!a.ReadSucceeded)?L["RecipeReadFailed"]:State==true?L["Enabled"]:State==false?L["Disabled"]:actual.All(a=>a.Snapshot==Snapshot.Default)?L["ToggleDefaultState"]:L["RecipeMixedState"];
    public bool CanToggle {get;private set;}
    public bool CanRestoreDefault=>CanToggle && actual.Any(a=>a.Snapshot!=Snapshot.Default);
    public string ResultText=>resultKey is null?"":L[resultKey];
    public string Evidence=>string.Join(" · ",Recipe.ObservedBuilds.Select(b=>b.Build.ToString(CultureInfo.InvariantCulture)+"."+(b.Ubr?.ToString(CultureInfo.InvariantCulture)??"?")+" ("+b.Channel+")"));
    public IEnumerable<string> Sources=>Recipe.Sources;
    public string ScopeDetails
    {
        get
        {
            var states = actual.Select(a => L.Format("RecipeScopeFormat",
                a.Id.ToString(CultureInfo.InvariantCulture),
                a.ReadSucceeded ? L.State(a.Snapshot!) : L["RecipeReadFailed"])
                + (a.Error is null ? "" : "\n" + a.Error.Message));
            var failures = results.Where(r => r.Error is not null)
                .Select(r => r.Change.Id.ToString(CultureInfo.InvariantCulture) + ": " + r.Error);
            return string.Join("\n", states.Concat(failures));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Update(IReadOnlyList<ImmediateToggleState> state,RecipeApplicability applicability,bool canAttempt)
    {
        actual=state;Applicability=applicability;CanToggle=canAttempt && applicability!=RecipeApplicability.NotApplicable && state.Count==Recipe.FeatureIds.Count && state.All(a=>a.ReadSucceeded);Refresh();
    }
    public void SetResult(string key,IReadOnlyList<ChangeResult> result){resultKey=key;results=result;Refresh();}
    public void Refresh()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
}
