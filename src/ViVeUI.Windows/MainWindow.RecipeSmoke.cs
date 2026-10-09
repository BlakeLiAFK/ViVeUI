using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using ViVeUI.Core;

namespace ViVeUI.Windows;

public partial class MainWindow
{
    // Opt-in native WPF checks. All mutations below use DemoStore and injected execution.
    public async Task RecipeSmokeAsync()
    {
        if (!demo || store is not DemoStore fake) throw new InvalidOperationException("Recipe smoke requires fake feature storage.");
        var expectedIds = new[] { "VoiceTypingFilter", "QuickAccessibilityGroups", "VoiceVocabulary", "VoiceQuickSettings", "SharePhotoEditing", "HdrSettingsHistory", "ExplorerAiActions" };
        var recipes = FeatureRecipes.All;
        var recipeIds = recipes.SelectMany(recipe => recipe.FeatureIds).Distinct().ToArray();
        var initialStates = recipeIds.ToDictionary(id => id, id => fake.Read(id));
        var originalController = recipeToggle;
        var originalBuild = demoDeviceBuild;
        var originalObservations = observations;
        var checks = new List<string>();
        var captures = new List<string>();
        var languages = new List<object>();
        var executions = new List<object>();
        string? failure = null;
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        async Task Shot(string name) { await Capture("recipes/" + name); captures.Add("previews/recipes/" + name); }
        async Task Settled()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (busy && timer.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(10);
            Require(!busy, "Recipe operation did not finish within the smoke-test budget.");
            await Idle();
        }
        RecipeCardModel Card(string id) => AllRecipeCards.Single(card => card.Recipe.Id == id);
        bool InitialStorage() => initialStates.All(pair => fake.Read(pair.Key) == pair.Value);
        void ObserveAll()
        {
            observations = recipeIds.ToDictionary(id => id, _ => "Default · User");
            observationError = null;
            RefreshRecipes();
        }
        async Task Filters(bool applicable, bool notApplicable, bool unconfirmed)
        {
            foreach (var (control, state) in new[] { (RecipeApplicableFilter, applicable), (RecipeNotApplicableFilter, notApplicable), (RecipeUnconfirmedFilter, unconfirmed) })
            {
                if (control.IsChecked != state)
                    ((IToggleProvider)new CheckBoxAutomationPeer(control).GetPattern(PatternInterface.Toggle)!).Toggle();
                await Idle();
                Require(control.IsChecked == state, "Recipe applicability checkbox did not update.");
            }
            Require(ShowApplicable == applicable && ShowNotApplicable == notApplicable && ShowUnconfirmed == unconfirmed, "Recipe filter bindings did not update their model.");
        }
        IEnumerable<DependencyObject> CardVisuals(RecipeCardModel card)
        {
            var container = RecipeItems.ItemContainerGenerator.ContainerFromItem(card) as DependencyObject;
            Require(container is not null, "Recipe card was not rendered: " + card.Recipe.Id);
            return Descendants(container!).Prepend(container!);
        }
        void VerifyRenderedCard(RecipeCardModel card)
        {
            var descendants = CardVisuals(card).ToArray();
            var text = descendants.OfType<TextBlock>().ToArray();
            Require(!string.IsNullOrWhiteSpace(card.Title) && text.Any(value => value.Text == card.Title), "Missing visible recipe name.");
            Require(!string.IsNullOrWhiteSpace(card.Purpose) && text.Any(value => value.Text == card.Purpose), "Missing visible recipe purpose.");
            var ids = descendants.OfType<TextBox>().Single(value => value.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path == nameof(RecipeCardModel.Ids));
            Require(ids.IsReadOnly && ids.Text == string.Join(", ", card.Recipe.FeatureIds.Select(id => id.ToString(CultureInfo.InvariantCulture))) && ids.FlowDirection == FlowDirection.LeftToRight, "Recipe IDs are missing, editable, or not invariant LTR text.");
            var toggleControl = descendants.OfType<ImmediateCheckBox>().Single();
            Require(toggleControl.Tag == card && toggleControl.IsChecked == card.State && toggleControl.GetBindingExpression(ToggleButton.IsCheckedProperty) is not null, "Recipe checkbox lost its actual-state binding.");
            Require(text.Any(value => value.Text == card.ApplicabilityText) && text.Any(value => value.Text == card.CurrentOverride), "Applicability and current override are not separately displayed.");
            var copy = descendants.OfType<Button>().Single(button => button.Tag == card && button.Content as string == L["RecipeCopyIds"]);
            RecipeCopyIdsClick(copy, new());
            Require(LastCopiedRecipeIds == card.Ids, "Copy action changed the recipe ID group.");
        }
        try
        {
            Require(IsRecipes && RecipePage.Visibility == Visibility.Visible, "Recipes are not the default main view.");
            Require(recipes.Count == 7 && recipes.Select(recipe => recipe.Id).SequenceEqual(expectedIds), "The seven named recipe fixtures changed.");
            Require(ShowApplicable && ShowUnconfirmed && !ShowNotApplicable, "Default recipe applicability filters changed.");
            Root.Width = 1440; Root.Height = 900; ThemeBox.SelectedIndex = 1; LanguageBox.SelectedValue = "en";
            demoDeviceBuild = new DeviceBuild(26200, null, "Demo"); observations = []; observationError = null; RefreshRecipes(); RecipeSearch = ""; UpdateLayoutMode(); ShowPage(RecipePage); await Idle();
            Require(RecipeCards.Select(card => card.Recipe.Id).SequenceEqual(expectedIds), "Default observed-device view does not expose all seven named recipes.");
            Require(AllRecipeCards.All(card => card.Recipe.FeatureIds.Count > 0 && CuratedCatalog.All.Single(entry => entry.Id == card.Recipe.Id).Kind == CuratedKind.Historical), "A native Settings shortcut was presented as a ViVe feature recipe.");
            Require(AllRecipeCards.All(card => card.Applicability == RecipeApplicability.Unconfirmed && card.State is null && card.CanToggle), "Missing configuration observations or UBR incorrectly hid or confirmed a recipe, fabricated override state, or denied a source-bounded attempt.");
            foreach (var card in RecipeCards) VerifyRenderedCard(card);
            await Shot("default-seven.png");
            RecipeScroll.ScrollToEnd(); await Shot("default-seven-last.png"); RecipeScroll.ScrollToTop();
            checks.Add("Default main view contains seven named recipes with purpose, actual ID group, copy action and bound checkbox; native Settings tasks are excluded.");

            ObserveAll();
            var exactRecipe = FeatureRecipes.Get("VoiceTypingFilter");
            demoDeviceBuild = exactRecipe.ObservedBuilds[0]; RefreshRecipes();
            Require(Card(exactRecipe.Id).Applicability == RecipeApplicability.Applicable, "Exact build, UBR and channel were not recognized.");
            demoDeviceBuild = new DeviceBuild(CurrentDeviceBuild.Build, null, CurrentDeviceBuild.Channel); RefreshRecipes();
            Require(Card(exactRecipe.Id).Applicability == RecipeApplicability.Unconfirmed, "Missing UBR was treated as an exact supported build.");
            demoDeviceBuild = exactRecipe.ObservedBuilds[0] with { Ubr = exactRecipe.ObservedBuilds[0].Ubr + 1 }; RefreshRecipes();
            Require(Card(exactRecipe.Id).Applicability == RecipeApplicability.Unconfirmed, "A later UBR was treated as a guaranteed supported range.");
            demoDeviceBuild = exactRecipe.ObservedBuilds[0];
            var missingRecipe = FeatureRecipes.Get("SharePhotoEditing"); observations.Remove(missingRecipe.FeatureIds[0]); RefreshRecipes();
            Require(Card(missingRecipe.Id).Applicability == RecipeApplicability.Unconfirmed && Card(missingRecipe.Id).CanToggle, "Configuration enumeration absence was mistaken for proof that a recipe is unavailable.");
            foreach (var device in new[] { exactRecipe.ObservedBuilds[0], new DeviceBuild(21999, 1, "Dev") })
            {
                demoDeviceBuild = device; RefreshRecipes();
                if (device.Build < 22000) Require(AllRecipeCards.All(card => card.Applicability == RecipeApplicability.NotApplicable && !card.CanToggle), "An unsupported Windows build exposed recipe mutation controls.");
            foreach (var applicable in new[] { false, true })
                foreach (var notApplicable in new[] { false, true })
                    foreach (var unconfirmed in new[] { false, true })
                    {
                        await Filters(applicable, notApplicable, unconfirmed);
                        var expected = AllRecipeCards.Where(card => card.Applicability switch { RecipeApplicability.Applicable => applicable, RecipeApplicability.NotApplicable => notApplicable, _ => unconfirmed }).Select(card => card.Recipe.Id);
                        Require(RecipeCards.Select(card => card.Recipe.Id).SequenceEqual(expected), "Applicability filtering changed ordering or included the wrong state.");
                        foreach (var (control, status) in new[] { (RecipeApplicableFilter, RecipeApplicability.Applicable), (RecipeNotApplicableFilter, RecipeApplicability.NotApplicable), (RecipeUnconfirmedFilter, RecipeApplicability.Unconfirmed) })
                        {
                            var expectedLabel = L["RecipeFilter" + status] + " (" + AllRecipeCards.Count(card => card.Applicability == status).ToString(CultureInfo.CurrentCulture) + ")";
                            Require(control.Content as string == expectedLabel, "Recipe applicability filter count label is incorrect.");
                        }
                        Require(InitialStorage(), "Applicability filtering wrote feature state.");
                    }
            }
            await Filters(true, false, true); ObserveAll();
            checks.Add("All eight applicability filter combinations and labels match on supported/unsupported build fixtures; exact UBR differs from unknown/newer UBR; absent configuration observations remain explicitly unconfirmed.");

            var group = FeatureRecipes.Get("SharePhotoEditing");
            demoDeviceBuild = group.ObservedBuilds[0]; RefreshRecipes(); RecipeSearch = Card(group.Id).Title; await Idle();
            int calls = 0;
            IReadOnlyList<Change> lastScope = [];
            string? lastRecipe = null;
            void Record(IReadOnlyList<Change> scope, string id, string mode)
            {
                calls++; lastScope = scope.ToArray(); lastRecipe = id;
                executions.Add(new { mode, recipe = id, ids = scope.Select(change => change.Id).ToArray(), targets = scope.Select(change => change.After.State.ToString()).ToArray() });
            }
            recipeToggle = new RecipeToggleController(fake, (scope, id, token) =>
            {
                token.ThrowIfCancellationRequested(); Record(scope, id, "apply");
                return Task.FromResult<IReadOnlyList<ChangeResult>>(ChangeEngine.Apply(fake, scope));
            });
            RefreshRecipes(); await Idle();
            var checkbox = CardVisuals(Card(group.Id)).OfType<ImmediateCheckBox>().Single();
            ((IToggleProvider)new CheckBoxAutomationPeer(checkbox).GetPattern(PatternInterface.Toggle)!).Toggle(); await Settled();
            Require(calls == 1 && lastRecipe == group.Id && lastScope.Select(change => change.Id).SequenceEqual(group.FeatureIds), "A grouped recipe did not use exactly one executor call with every ID.");
            // RefreshRecipeView publishes a new ItemsSource; the clicked control may
            // have been detached while its replacement receives the verified state.
            var liveCard = Card(group.Id);
            var liveCheckbox = CardVisuals(liveCard).OfType<ImmediateCheckBox>().Single();
            var liveBinding = liveCheckbox.GetBindingExpression(ToggleButton.IsCheckedProperty);
            var actualGroup = group.FeatureIds.Select(id => new { id, snapshot = fake.Read(id) }).ToArray();
            var readbackDiagnostics = JsonSerializer.Serialize(new
            {
                actual = actualGroup, modelState = liveCard.State, liveCheckboxState = liveCheckbox.IsChecked,
                clickedCheckboxState = checkbox.IsChecked, controlReplaced = !ReferenceEquals(checkbox, liveCheckbox),
                bindingPath = liveBinding?.ParentBinding.Path?.Path, bindingStatus = liveBinding?.Status.ToString(),
                dataContextMatches = ReferenceEquals(liveCheckbox.DataContext, liveCard), tagMatches = ReferenceEquals(liveCheckbox.Tag, liveCard)
            });
            Require(actualGroup.All(item => item.snapshot == new Snapshot(true, OverrideState.Enabled)) &&
                liveCard.State == true && liveCheckbox.IsChecked == true && liveBinding is not null &&
                liveBinding.ParentBinding.Path?.Path == nameof(RecipeCardModel.State) &&
                liveBinding.Status == System.Windows.Data.BindingStatus.Active &&
                ReferenceEquals(liveCheckbox.DataContext, liveCard) && ReferenceEquals(liveCheckbox.Tag, liveCard),
                "Successful grouped readback did not drive the live checkbox: " + readbackDiagnostics);
            Require(Card(group.Id).Applicability == RecipeApplicability.Applicable && Card(group.Id).CurrentOverride == L["Enabled"], "Enabling an override overwrote its independent applicability state.");
            await Shot("group-enabled.png");
            demoDeviceBuild = group.ObservedBuilds[0] with { Ubr = null }; RefreshRecipes();
            Require(Card(group.Id).State == true && Card(group.Id).Applicability == RecipeApplicability.Unconfirmed && Card(group.Id).CurrentOverride == L["Enabled"], "Unknown applicability was confused with a disabled/default override.");
            demoDeviceBuild = group.ObservedBuilds[0]; RefreshRecipes();
            await ChangeRecipeAsync(Card(group.Id), Snapshot.Default); await Idle();
            Require(calls == 2 && lastScope.Count == group.FeatureIds.Count && group.FeatureIds.All(id => fake.Read(id) == Snapshot.Default) && Card(group.Id).State is null, "Grouped Default did not reset every ID together.");
            await ChangeRecipeAsync(Card(group.Id), Snapshot.Default);
            Require(calls == 2, "An unchanged group caused redundant execution.");
            checks.Add("The two-ID recipe uses one executor call for the full group; verified readback controls On/Default independently from applicability.");

            recipeToggle = new RecipeToggleController(fake, (scope, id, token) =>
            {
                Record(scope, id, "reported-success-without-write");
                return Task.FromResult<IReadOnlyList<ChangeResult>>(scope.Select(change => new ChangeResult(change, true, null)).ToArray());
            });
            RefreshRecipes(); var beforeFailure = calls;
            await ChangeRecipeAsync(Card(group.Id), new Snapshot(true, OverrideState.Enabled)); await Idle();
            Require(calls == beforeFailure + 1 && group.FeatureIds.All(id => fake.Read(id) == Snapshot.Default) && Card(group.Id).State is null && Card(group.Id).ResultText == L["RecipeResultFailed"], "Reported success without matching readback became an optimistic On.");
            await Shot("group-readback-conflict.png");
            recipeToggle = new RecipeToggleController(fake, (scope, id, token) =>
            {
                Record(scope, id, "canceled");
                return Task.FromException<IReadOnlyList<ChangeResult>>(new OperationCanceledException("Injected recipe cancellation."));
            });
            RefreshRecipes(); var beforeCancel = calls;
            await ChangeRecipeAsync(Card(group.Id), new Snapshot(true, OverrideState.Enabled)); await Idle();
            Require(calls == beforeCancel + 1 && lastError is OperationCanceledException && group.FeatureIds.All(id => fake.Read(id) == Snapshot.Default) && Card(group.Id).State is null, "Cancellation changed actual recipe state.");
            await Shot("group-canceled.png");
            recipeToggle = new RecipeToggleController(fake, (scope, id, token) =>
            {
                Record(scope, id, "partial-failure");
                return Task.FromResult<IReadOnlyList<ChangeResult>>(ChangeEngine.Apply(fake, scope));
            });
            fake.FailOn = group.FeatureIds[1]; RefreshRecipes();
            await ChangeRecipeAsync(Card(group.Id), new Snapshot(true, OverrideState.Enabled)); await Idle();
            Require(fake.Read(group.FeatureIds[0]) == new Snapshot(true, OverrideState.Enabled) && fake.Read(group.FeatureIds[1]) == Snapshot.Default && Card(group.Id).State is null && Card(group.Id).CurrentOverride == L["RecipeMixedState"], "Partial failure concealed a mixed group state.");
            Require(group.FeatureIds.All(id => Card(group.Id).ScopeDetails.Contains(id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)), "Failure details dropped part of the recipe scope.");
            await Shot("group-partial.png"); fake.FailOn = 0;
            await ChangeRecipeAsync(Card(group.Id), Snapshot.Default);
            Require(group.FeatureIds.All(id => fake.Read(id) == Snapshot.Default), "The fake partial-write fixture could not be restored.");
            checks.Add("Readback conflicts, cancellation and partial failures show actual Default/mixed states, with every ID retained in the details.");

            demoDeviceBuild = new DeviceBuild(26200, null, "Demo"); ObserveAll(); RecipeSearch = "";
            foreach (var language in Localization.Languages)
            {
                LanguageBox.SelectedValue = language.Code; RefreshRecipes(); await Idle();
                Require(L.Language == language.Code && Root.FlowDirection == (language.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight), "Recipe language direction did not update.");
                Require(ShowApplicable && !ShowNotApplicable && ShowUnconfirmed && RecipeCards.Count() == 7, "Language switching changed recipe visibility filters.");
                foreach (var card in RecipeCards)
                {
                    Require(card.Purpose == L["RecipePurpose" + card.Recipe.Id], "Recipe purpose did not use its localized resource.");
                    VerifyRenderedCard(card);
                }
                var searchCard = Card(group.Id); RecipeSearch = "  " + searchCard.Title + "  ";
                Require(RecipeCards.Any(card => card.Recipe.Id == group.Id), "Localized recipe search did not trim whitespace.");
                RecipeSearch = ""; Root.Width = 1440; Root.Height = 900; UpdateLayoutMode(); RecipeScroll.ScrollToTop();
                await Shot(language.Code + "/full.png");
                Root.Width = 900; UpdateLayoutMode(); await Shot(language.Code + "/compact.png");
                Require(InitialStorage(), "Recipe localization or copy controls changed feature storage.");
                languages.Add(new { language = language.Code, recipes = RecipeCards.Count(), rtl = language.IsRightToLeft, idsLeftToRight = true, copyVerified = true, localizedPurpose = true, compactRender = true });
            }
            checks.Add("All sixteen locales show localized purpose and state labels, invariant LTR IDs, copy controls, trimmed search and full/compact WPF captures.");
        }
        catch (Exception error) { failure = error.Message; throw; }
        finally
        {
            fake.FailOn = 0;
            foreach (var (id, state) in initialStates) fake.Write(id, state);
            recipeToggle = originalController; demoDeviceBuild = originalBuild; observations = originalObservations; RefreshRecipes();
            File.WriteAllText("recipe-result.json", JsonSerializer.Serialize(new
            {
                passed = failure is null, failure, recipeCount = recipes.Count, recipeIds = recipes.Select(recipe => recipe.Id).ToArray(),
                checks, executions, languages, captures, fakeBackendOnly = true, noNativeSettingsModified = true, humanVisualReviewRequired = true
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
