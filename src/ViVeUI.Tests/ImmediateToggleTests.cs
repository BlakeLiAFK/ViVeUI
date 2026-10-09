using ViVeUI.Core;

public static class ImmediateToggleTests
{
    static readonly Feature Demo = new(4294967201, "Demo immediate toggle");
    static readonly Snapshot Enabled = new(true, OverrideState.Enabled);
    static readonly Snapshot Disabled = new(true, OverrideState.Disabled);
    static void Assert(bool value) { if (!value) throw new Exception("Immediate toggle assertion failed."); }
    static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static async Task<int> RunAsync()
    {
        int passed = 0;
        async Task Test(string name, Func<Task> test)
        {
            try { await test(); passed++; Console.WriteLine("PASS Immediate: " + name); }
            catch (Exception error) { throw new Exception(name, error); }
        }
        await Test("Default and failed reads remain indeterminate", () =>
        {
            var store = new Store(); var controller = Controller(store);
            var state = controller.Read(Demo.Id);
            Assert(state.ReadSucceeded && state.State is null && state.Snapshot == Snapshot.Default);
            store.Value = new(true, OverrideState.Default);
            Assert(controller.Read(Demo.Id).State is null);
            store.ReadFailure = true;
            state = controller.Read(Demo.Id);
            Assert(!state.ReadSucceeded && state.State is null && state.Snapshot is null && state.Error is IOException);
            return Task.CompletedTask;
        });
        await Test("One unknown feature applies and refreshes actual state", async () =>
        {
            var store = new Store(); IReadOnlyList<Change>? captured = null;
            var controller = new ImmediateToggleController(store, (changes, _) =>
            {
                captured = changes;
                return Task.FromResult<IReadOnlyList<ChangeResult>>(ChangeEngine.Apply(store, changes));
            });
            Assert(!CuratedCatalog.IsHistoricalReference(Demo.Id));
            var result = await controller.ApplyAsync(Demo, Enabled);
            Assert(result.Succeeded && result.Actual.State == true && result.Actual.Snapshot == Enabled);
            Assert(captured is { Count: 1 } && captured[0].Id == Demo.Id && captured[0].Before == Snapshot.Default);
            Assert(result.Change == captured![0] && result.Results.Single().Applied && !controller.IsBusy && store.Writes == 1);
            result = await controller.ApplyAsync(Demo, Disabled);
            Assert(result.Succeeded && result.Actual.State == false && result.Change!.Before == Enabled);
        });
        await Test("Reported failure never displays the requested state", async () =>
        {
            var store = new Store();
            var controller = new ImmediateToggleController(store, (changes, _) =>
                Task.FromResult<IReadOnlyList<ChangeResult>>([new(changes[0], false, "Denied")]));
            var result = await controller.ApplyAsync(Demo, Enabled);
            Assert(!result.Succeeded && result.Error is IOException && result.Actual.State is null && store.Writes == 0);
            Assert(result.Results.Single().Error == "Denied" && !controller.IsBusy);
        });
        await Test("Exception after a write refreshes the real override", async () =>
        {
            var store = new Store();
            var controller = new ImmediateToggleController(store, (_, _) =>
            {
                store.Write(Demo.Id, Enabled);
                throw new IOException("Transport failed after writing");
            });
            var result = await controller.ApplyAsync(Demo, Enabled);
            Assert(!result.Succeeded && result.Error is IOException && result.Actual.State == true && !controller.IsBusy);
        });
        await Test("Cancellation refreshes after a partially completed backend", async () =>
        {
            var store = new Store(); using var cancellation = new CancellationTokenSource();
            var controller = new ImmediateToggleController(store, (_, token) =>
            {
                store.Write(Demo.Id, Disabled); cancellation.Cancel();
                return Task.FromCanceled<IReadOnlyList<ChangeResult>>(token);
            });
            var result = await controller.ApplyAsync(Demo, Enabled, cancellation.Token);
            Assert(result.Canceled && !result.Succeeded && result.Actual.State == false && !controller.IsBusy);
        });
        await Test("Pre-cancellation performs no mutation", async () =>
        {
            var store = new Store(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var result = await Controller(store).ApplyAsync(Demo, Enabled, cancellation.Token);
            Assert(result.Canceled && result.Actual.ReadSucceeded && result.Actual.State is null && store.Writes == 0);
        });
        await Test("Atomic busy gate rejects duplicates and captures original feature", async () =>
        {
            var store = new Store(); var entered = Gate(); var release = Gate(); int calls = 0;
            var controller = new ImmediateToggleController(store, async (changes, _) =>
            {
                calls++; entered.SetResult(); await release.Task;
                Assert(changes.Count == 1 && changes[0].Id == Demo.Id && changes[0].Before == Snapshot.Default);
                return ChangeEngine.Apply(store, changes);
            });
            var operation = controller.ApplyAsync(Demo, Enabled); await entered.Task;
            Assert(controller.IsBusy && controller.Read(Demo.Id).State is null);
            bool rejected = false;
            try { await controller.ApplyAsync(new(4294967202, "Demo second"), Disabled); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && calls == 1 && store.Writes == 0);
            release.SetResult(); var result = await operation;
            Assert(result.Succeeded && result.Actual.Id == Demo.Id && !controller.IsBusy && store.Writes == 1);
        });
        await Test("Conflict after await preserves the intervening state", async () =>
        {
            var store = new Store(); var entered = Gate(); var release = Gate();
            var controller = new ImmediateToggleController(store, async (changes, _) =>
            { entered.SetResult(); await release.Task; return ChangeEngine.Apply(store, changes); });
            var operation = controller.ApplyAsync(Demo, Enabled); await entered.Task;
            store.Value = Disabled; release.SetResult(); var result = await operation;
            Assert(!result.Succeeded && result.Error is InvalidOperationException && result.Actual.State == false && store.Writes == 0);
        });
        await Test("Default recovery removes an existing override", async () =>
        {
            var store = new Store { Value = Enabled }; var controller = Controller(store);
            var result = await controller.ApplyAsync(Demo, Snapshot.Default);
            Assert(result.Succeeded && result.Actual.State is null && result.Actual.Snapshot == Snapshot.Default && store.Writes == 1);
            result = await controller.ApplyAsync(Demo, Snapshot.Default);
            Assert(result.Succeeded && result.Results.Count == 0 && store.Writes == 1);
        });
        await Test("Historical targets reject all overrides including explicit Default", async () =>
        {
            var id = CuratedCatalog.All.Where(e => e.Kind == CuratedKind.Historical).SelectMany(e => e.FeatureIds).First();
            var store = new Store(); var controller = Controller(store); var feature = new Feature(id, "Historical fixture");
            foreach (var target in new[] { Enabled, Disabled, new Snapshot(true, OverrideState.Default) })
            {
                var result = await controller.ApplyAsync(feature, target);
                Assert(!result.Succeeded && result.Error is InvalidDataException && store.Writes == 0);
            }
            store.Value = Enabled;
            var recovery = await controller.ApplyAsync(feature, Snapshot.Default);
            Assert(recovery.Succeeded && recovery.Actual.State is null && store.Writes == 1);
        });
        await Test("Read failure blocks writes and remains indeterminate", async () =>
        {
            var store = new Store { ReadFailure = true }; var result = await Controller(store).ApplyAsync(Demo, Enabled);
            Assert(!result.Succeeded && result.Change is null && result.Error is IOException && !result.Actual.ReadSucceeded && result.Actual.State is null && store.Writes == 0);
        });
        await Test("Backend success requires an actual matching readback", async () =>
        {
            var store = new Store();
            var controller = new ImmediateToggleController(store, (changes, _) => Task.FromResult<IReadOnlyList<ChangeResult>>([new(changes[0], true, null)]));
            var result = await controller.ApplyAsync(Demo, Enabled);
            Assert(!result.Succeeded && result.Error is IOException && result.Actual.State is null);
        });
        await Test("Readback failure cannot produce an optimistic enabled state", async () =>
        {
            var store = new Store();
            var controller = new ImmediateToggleController(store, (changes, _) =>
            {
                var results = ChangeEngine.Apply(store, changes); store.ReadFailure = true;
                return Task.FromResult<IReadOnlyList<ChangeResult>>(results);
            });
            var result = await controller.ApplyAsync(Demo, Enabled);
            Assert(!result.Succeeded && result.Error is IOException && result.Actual.State is null && !result.Actual.ReadSucceeded);
        });
        await Test("Backend cannot substitute a different feature in results", async () =>
        {
            var store = new Store();
            var controller = new ImmediateToggleController(store, (changes, _) => Task.FromResult<IReadOnlyList<ChangeResult>>([new(changes[0] with { Id = 4294967202 }, true, null)]));
            var result = await controller.ApplyAsync(Demo, Enabled);
            Assert(!result.Succeeded && result.Error is InvalidDataException && result.Actual.Id == Demo.Id);
        });
        return passed;
    }
    static ImmediateToggleController Controller(Store store) => new(store,
        (changes, _) => Task.FromResult<IReadOnlyList<ChangeResult>>(ChangeEngine.Apply(store, changes)));
    sealed class Store : IFeatureStore
    {
        public Snapshot Value = Snapshot.Default;
        public bool ReadFailure;
        public int Writes;
        public Snapshot Read(uint id) => ReadFailure ? throw new IOException("Read unavailable") : Value;
        public void Write(uint id, Snapshot state) { Writes++; Value = state; }
    }
}
