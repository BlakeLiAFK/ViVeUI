using ViVeUI.Core;

public static class RecipeTests
{
    static readonly Snapshot Enabled = new(true, OverrideState.Enabled);
    static readonly Snapshot Disabled = new(true, OverrideState.Disabled);
    static void Assert(bool condition) { if (!condition) throw new Exception("Recipe assertion failed."); }
    static void Reject(Action action)
    { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected recipe rejection."); }
    static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    static Change[] Changes(FeatureRecipe recipe, Snapshot target) => recipe.FeatureIds.Select(id => new Change(id, recipe.Title, Snapshot.Default, target)).ToArray();

    public static async Task<int> RunAsync()
    {
        int passed = 0;
        async Task Test(string name, Func<Task> action)
        { try { await action(); passed++; Console.WriteLine("PASS Recipe: " + name); } catch (Exception error) { throw new Exception(name, error); } }
        var pair = FeatureRecipes.Get("SharePhotoEditing");
        var build = pair.ObservedBuilds[0];
        var observed = pair.FeatureIds.ToHashSet();
        await Test("Seven primary-sourced recipes preserve real groups and raw archival guard", () =>
        {
            Assert(FeatureRecipes.All.Count == 7 && FeatureRecipes.All.SelectMany(recipe => recipe.FeatureIds).Distinct().Count() == 9);
            Assert(pair.FeatureIds.SequenceEqual(new uint[] { 53433910, 54475355 }));
            Assert(FeatureRecipes.Get("ExplorerAiActions").FeatureIds.SequenceEqual(new uint[] { 54792954, 55345819 }));
            foreach (var recipe in FeatureRecipes.All)
            {
                Assert(recipe.Sources.Count >= 2 && recipe.Sources.All(source => new Uri(source).Scheme == "https"));
                Assert(recipe.ObservedBuilds.All(device => device.Ubr > 0 && device.Channel is not null));
                foreach (var id in recipe.FeatureIds) Reject(() => CuratedCatalog.ValidateOverrideMutation(id, Enabled));
            }
            Reject(() => FeatureRecipes.Get("ExplorerTabsHistory"));
            return Task.CompletedTask;
        });
        await Test("Exact observed revisions and channel are distinguished from unconfirmed builds", () =>
        {
            foreach (var recipe in FeatureRecipes.All)
            {
                var ids = recipe.FeatureIds.ToHashSet(); var hardware = new Dictionary<string, bool?> { [FeatureRecipes.HdrDisplay] = true };
                foreach (var device in recipe.ObservedBuilds)
                    Assert(FeatureRecipes.Evaluate(recipe, device, ids, hardware) == RecipeApplicability.Applicable);
                var original = recipe.ObservedBuilds[0];
                foreach (var device in new[] { original with { Ubr = null }, original with { Ubr = 0 }, original with { Ubr = original.Ubr + 1 }, original with { Channel = null }, original with { Channel = "Retail" }, original with { Build = 26300 } })
                    Assert(FeatureRecipes.Evaluate(recipe, device, ids, hardware) == RecipeApplicability.Unconfirmed);
            }
            return Task.CompletedTask;
        });
        await Test("Absent queried IDs mean unconfirmed rather than unsupported", () =>
        {
            Assert(FeatureRecipes.Evaluate(pair, build, new HashSet<uint>()) == RecipeApplicability.Unconfirmed);
            FeatureRecipes.ValidateScope(pair.Id, Changes(pair, Enabled), build, new HashSet<uint>());
            Assert(FeatureRecipes.Evaluate(pair, new(19045, 100, "Retail"), observed) == RecipeApplicability.NotApplicable);
            Reject(() => FeatureRecipes.ValidateScope(pair.Id, Changes(pair, Enabled), new(19045, 100), observed));
            return Task.CompletedTask;
        });
        await Test("Unknown hardware remains pending while known unsupported hardware is rejected", () =>
        {
            var hdr = FeatureRecipes.Get("HdrSettingsHistory"); var ids = hdr.FeatureIds.ToHashSet(); var device = hdr.ObservedBuilds[0];
            Assert(FeatureRecipes.Evaluate(hdr, device, ids) == RecipeApplicability.Unconfirmed);
            var unsupported = new Dictionary<string, bool?> { [FeatureRecipes.HdrDisplay] = false };
            Assert(FeatureRecipes.Evaluate(hdr, device, ids, unsupported) == RecipeApplicability.NotApplicable);
            Reject(() => FeatureRecipes.ValidateScope(hdr.Id, Changes(hdr, Enabled), device, ids, unsupported));
            FeatureRecipes.ValidateScope(hdr.Id, Changes(hdr, Enabled), device, ids);
            return Task.CompletedTask;
        });
        await Test("Scope rejects missing extra duplicate mixed and untrusted changes", () =>
        {
            var valid = Changes(pair, Enabled);
            Reject(() => FeatureRecipes.ValidateScope("InventedRecipe", valid, build, observed));
            Reject(() => FeatureRecipes.ValidateScope(pair.Id, valid[..1], build, observed));
            Reject(() => FeatureRecipes.ValidateScope(pair.Id, [..valid, new(4294967201, "Extra", Snapshot.Default, Enabled)], build, observed));
            Reject(() => FeatureRecipes.ValidateScope(pair.Id, [valid[0], valid[0]], build, observed));
            Reject(() => FeatureRecipes.ValidateScope(pair.Id, [valid[0], valid[1] with { After = Disabled }], build, observed));
            Reject(() => FeatureRecipes.ValidateScope(pair.Id, Changes(pair, new(true, OverrideState.Default)), build, observed));
            Reject(() => FeatureRecipes.ValidateScope(pair.Id, Changes(pair, new(false, OverrideState.Enabled)), build, observed));
            foreach (var target in new[] { Enabled, Disabled, Snapshot.Default }) FeatureRecipes.ValidateScope(pair.Id, Changes(pair, target), build, observed);
            return Task.CompletedTask;
        });
        await Test("One call contains every dependency and returns every actual state", async () =>
        {
            var store = new Store(); store.Values[pair.FeatureIds[0]] = Enabled; int calls = 0;
            var controller = new RecipeToggleController(store, (changes, id, _) =>
            {
                calls++; Assert(id == pair.Id && changes.Count == 2 && changes[0].Before == Enabled);
                FeatureRecipes.ValidateScope(id, changes, build, observed);
                return Task.FromResult<IReadOnlyList<ChangeResult>>(ChangeEngine.Apply(store, changes));
            });
            var result = await controller.ApplyAsync(pair.Id, Enabled, build, observed);
            Assert(result.Succeeded && result.State == true && calls == 1 && result.Changes.Count == 2 && result.Results.Count == 2 && result.Actual.Count == 2);
            Assert(!controller.IsBusy && result.Actual.All(actual => actual.ReadSucceeded));
            result = await controller.ApplyAsync(pair.Id, Snapshot.Default, build, observed);
            Assert(result.Succeeded && result.State is null && result.Actual.All(actual => actual.Snapshot == Snapshot.Default));
        });
        await Test("Pending sourced recipe can be tried without fabricated observed IDs", async () =>
        {
            var store = new Store(); var pending = new DeviceBuild(26200, null); var ids = new HashSet<uint>();
            var controller = Controller(store, pending, ids);
            var result = await controller.ApplyAsync(pair.Id, Enabled, pending, ids);
            Assert(result.Succeeded && result.State == true);
            Assert(FeatureRecipes.Evaluate(pair, pending, ids) == RecipeApplicability.Unconfirmed);
        });
        await Test("Partial failure preserves applied failed and unread target states", async () =>
        {
            var store = new Store { FailWrite = pair.FeatureIds[1] };
            var controller = Controller(store, build, observed);
            var result = await controller.ApplyAsync(pair.Id, Enabled, build, observed);
            Assert(!result.Succeeded && result.Error is IOException && result.State is null && result.Results.Count == 2);
            Assert(result.Results[0].Applied && !result.Results[1].Applied);
            Assert(result.Actual[0].State == true && result.Actual[1].State is null && !controller.IsBusy);
        });
        await Test("Cancellation after partial write still reads every ID", async () =>
        {
            var store = new Store(); using var cancellation = new CancellationTokenSource();
            var controller = new RecipeToggleController(store, (changes, _, token) =>
            { store.Write(changes[0].Id, changes[0].After); cancellation.Cancel(); return Task.FromCanceled<IReadOnlyList<ChangeResult>>(token); });
            var result = await controller.ApplyAsync(pair.Id, Enabled, build, observed, cancellationToken: cancellation.Token);
            Assert(result.Canceled && !result.Succeeded && result.Actual.Count == 2 && result.Actual[0].State == true && result.Actual[1].State is null && result.State is null);
            Assert(!controller.IsBusy);
        });
        await Test("Atomic gate captures group and rejects a second concurrent operation", async () =>
        {
            var store = new Store(); var entered = Gate(); var release = Gate(); int calls = 0;
            var controller = new RecipeToggleController(store, async (changes, id, _) =>
            { calls++; entered.SetResult(); await release.Task; Assert(id == pair.Id && changes.Select(change => change.Id).SequenceEqual(pair.FeatureIds)); return ChangeEngine.Apply(store, changes); });
            var pending = controller.ApplyAsync(pair.Id, Enabled, build, observed); await entered.Task;
            Assert(controller.IsBusy && controller.Read(pair.Id).All(actual => actual.State is null));
            bool rejected = false;
            try { await controller.ApplyAsync("VoiceTypingFilter", Disabled, build, observed); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && calls == 1 && store.Writes == 0); release.SetResult();
            Assert((await pending).Succeeded && !controller.IsBusy);
        });
        await Test("Group preflight conflict makes zero writes and refreshes intervening state", async () =>
        {
            var store = new Store(); var entered = Gate(); var release = Gate();
            var controller = new RecipeToggleController(store, async (changes, _, _) =>
            { entered.SetResult(); await release.Task; return ChangeEngine.Apply(store, changes); });
            var pending = controller.ApplyAsync(pair.Id, Enabled, build, observed); await entered.Task;
            store.Values[pair.FeatureIds[1]] = Disabled; release.SetResult(); var result = await pending;
            Assert(!result.Succeeded && result.Error is InvalidOperationException && store.Writes == 0 && result.Actual[1].State == false);
        });
        await Test("Read failure blocks the entire group with no optimistic state", async () =>
        {
            var store = new Store { FailRead = pair.FeatureIds[1] }; var controller = Controller(store, build, observed);
            var result = await controller.ApplyAsync(pair.Id, Enabled, build, observed);
            Assert(!result.Succeeded && result.Error is IOException && result.Changes.Count == 0 && store.Writes == 0 && result.State is null && !result.Actual[1].ReadSucceeded);
        });
        await Test("Reported set success alone cannot satisfy group readback", async () =>
        {
            var store = new Store();
            var controller = new RecipeToggleController(store, (changes, _, _) => Task.FromResult<IReadOnlyList<ChangeResult>>(changes.Select(change => new ChangeResult(change, true, null)).ToArray()));
            var result = await controller.ApplyAsync(pair.Id, Enabled, build, observed);
            Assert(!result.Succeeded && result.Error is IOException && result.State is null && result.Results.All(item => item.Applied));
        });
        await Test("Forged backend scope and empty response are rejected", async () =>
        {
            var store = new Store();
            var forged = new RecipeToggleController(store, (changes, _, _) => Task.FromResult<IReadOnlyList<ChangeResult>>([new(changes[0] with { Id = 4294967201 }, true, null)]));
            Assert((await forged.ApplyAsync(pair.Id, Enabled, build, observed)).Error is InvalidDataException);
            var empty = new RecipeToggleController(store, (_, _, _) => Task.FromResult<IReadOnlyList<ChangeResult>>([]));
            Assert((await empty.ApplyAsync(pair.Id, Enabled, build, observed)).Error is InvalidDataException);
        });
        await Test("Already-default group is a no-op while pre-cancel never executes", async () =>
        {
            var store = new Store(); int calls = 0;
            var controller = new RecipeToggleController(store, (_, _, _) => { calls++; throw new Exception("Should not execute"); });
            var result = await controller.ApplyAsync(pair.Id, Snapshot.Default, build, observed);
            Assert(result.Succeeded && result.State is null && result.Changes.Count == 2 && result.Results.Count == 0 && calls == 0);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            result = await controller.ApplyAsync(pair.Id, Enabled, build, observed, cancellationToken: cancellation.Token);
            Assert(result.Canceled && result.Actual.Count == 2 && calls == 0 && store.Writes == 0);
        });
        return passed;
    }

    static RecipeToggleController Controller(Store store, DeviceBuild device, IReadOnlySet<uint> observed) => new(store, (changes, recipeId, _) =>
    {
        FeatureRecipes.ValidateScope(recipeId, changes, device, observed);
        return Task.FromResult<IReadOnlyList<ChangeResult>>(ChangeEngine.Apply(store, changes));
    });
    sealed class Store : IFeatureStore
    {
        public Dictionary<uint, Snapshot> Values = [];
        public uint FailRead, FailWrite;
        public int Writes;
        public Snapshot Read(uint id) => id == FailRead ? throw new IOException("Read denied") : Values.GetValueOrDefault(id, Snapshot.Default);
        public void Write(uint id, Snapshot value)
        { if (id == FailWrite) throw new IOException("Write denied"); Writes++; Values[id] = value; }
    }
}
