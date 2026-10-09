using ViVeUI.Core;

public static class ManualIdInspectorTests
{
    static void Assert(bool value) { if (!value) throw new Exception("Manual ID assertion failed."); }
    static TaskCompletionSource<Snapshot> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static async Task<int> RunAsync()
    {
        int passed = 0;
        async Task Test(string name, Func<Task> test)
        {
            try { await test(); passed++; Console.WriteLine("PASS Manual ID: " + name); }
            catch (Exception error) { throw new Exception(name, error); }
        }
        await Test("Strict nonzero uint decimal parsing", async () =>
        {
            int calls = 0;
            using var inspector = new ManualIdInspector((_, _) => { calls++; return Task.FromResult(Snapshot.Default); });
            foreach (var text in new[] { "", " \t\n", "0", "000", "-1", "+1", "4294967296", "1 2", "1\t2", "1.0", "0x42", "١٢", "１２", null! })
            {
                bool rejected = false;
                try { await inspector.QueryAsync(text); } catch (FormatException) { rejected = true; }
                Assert(rejected);
            }
            Assert(calls == 0);
            foreach (var text in new[] { "1", " \t4294967295\r\n", "00042" })
            {
                var result = await inspector.QueryAsync(text);
                Assert(result is not null && result.Error is null && inspector.IsCurrent(result));
            }
            Assert(calls == 3 && (await inspector.QueryAsync("4294967295"))!.Id == uint.MaxValue);
        });
        await Test("Older completion cannot replace newer ID", async () =>
        {
            var older = Gate(); CancellationToken oldToken = default;
            using var inspector = new ManualIdInspector((id, token) => { if (id == 1) { oldToken = token; return older.Task; } return Task.FromResult(new Snapshot(true, OverrideState.Enabled)); });
            var first = inspector.QueryAsync("1"); var latest = await inspector.QueryAsync("2");
            Assert(oldToken.IsCancellationRequested && latest?.Id == 2 && inspector.IsCurrent(latest));
            older.SetResult(Snapshot.Default); Assert(await first is null && inspector.IsCurrent(latest));
        });
        await Test("Repeated identical IDs still have distinct generations", async () =>
        {
            var older = Gate(); int reads = 0;
            using var inspector = new ManualIdInspector((_, _) => ++reads == 1 ? older.Task : Task.FromResult(new Snapshot(true, OverrideState.Disabled)));
            var first = inspector.QueryAsync("42"); var second = await inspector.QueryAsync("42");
            older.SetResult(new(true, OverrideState.Enabled));
            Assert(await first is null && second?.Snapshot == new Snapshot(true, OverrideState.Disabled));
            var third = await inspector.QueryAsync("42"); Assert(!inspector.IsCurrent(second) && inspector.IsCurrent(third));
        });
        await Test("Invalid input invalidates pending and already delivered results", async () =>
        {
            var older = Gate();
            using var inspector = new ManualIdInspector((id, _) => id == 1 ? older.Task : Task.FromResult(Snapshot.Default));
            var delivered = await inspector.QueryAsync("2"); var first = inspector.QueryAsync("1");
            try { await inspector.QueryAsync("-1"); } catch (FormatException) { }
            older.SetResult(Snapshot.Default);
            Assert(await first is null && !inspector.IsCurrent(delivered));
        });
        await Test("Dispose invalidates, cancels and suppresses late reads", async () =>
        {
            var older = Gate(); CancellationToken token = default; int calls = 0;
            var inspector = new ManualIdInspector((_, cancellation) => { calls++; token = cancellation; return older.Task; });
            var pending = inspector.QueryAsync("42"); inspector.Dispose(); inspector.Dispose();
            Assert(token.IsCancellationRequested); older.SetResult(Snapshot.Default);
            Assert(await pending is null && await inspector.QueryAsync("43") is null && await inspector.QueryAsync("invalid") is null && calls == 1);
        });
        await Test("Default snapshot differs from read failure", async () =>
        {
            using var inspector = new ManualIdInspector((id, _) => id == 1 ? Task.FromResult(Snapshot.Default) : throw new IOException("Cannot read"));
            var absent = await inspector.QueryAsync("1"); var failed = await inspector.QueryAsync("2");
            Assert(absent?.Snapshot == Snapshot.Default && absent.Error is null);
            Assert(failed?.Snapshot is null && failed?.Error is IOException && inspector.IsCurrent(failed));
            Assert(!inspector.IsCurrent(absent)); inspector.Dispose(); Assert(!inspector.IsCurrent(failed));
        });
        await Test("Canceled reader produces no result", async () =>
        {
            using var inspector = new ManualIdInspector((_, _) => Task.FromCanceled<Snapshot>(new CancellationToken(true)));
            Assert(await inspector.QueryAsync("1") is null);
        });
        await Test("Invalid snapshots are errors, never defaults", async () =>
        {
            foreach (var snapshot in new Snapshot[] { null!, new(false, OverrideState.Enabled), new(true, (OverrideState)99) })
            {
                using var inspector = new ManualIdInspector((_, _) => Task.FromResult(snapshot));
                var result = await inspector.QueryAsync("42");
                Assert(result?.Snapshot is null && result?.Error is InvalidDataException);
            }
        });
        await Test("Results from another inspector are never current", async () =>
        {
            using var first = new ManualIdInspector((_, _) => Task.FromResult(Snapshot.Default));
            using var second = new ManualIdInspector((_, _) => Task.FromResult(Snapshot.Default));
            var result = await first.QueryAsync("1"); await second.QueryAsync("1");
            Assert(first.IsCurrent(result) && !second.IsCurrent(result) && !first.IsCurrent(null));
        });
        return passed;
    }
}
