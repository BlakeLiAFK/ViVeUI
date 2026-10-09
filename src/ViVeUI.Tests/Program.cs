using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ViVeUI.Core;

int passed = 0;
void Test(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { throw new Exception(name, e); } }
async Task AsyncTest(string name, Func<Task> test) { try { await test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { throw new Exception(name, e); } }
void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Throws<T>(Action test) where T : Exception { try { test(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
async Task ThrowsAsync<T>(Func<Task> test) where T : Exception { try { await test(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
var catalog = Catalog.Load();
Test("Embedded catalog exact pinned SHA-256", () => { using var input = typeof(Catalog).Assembly.GetManifestResourceStream("ViVeUI.Core.FeatureDictionary.pfs")!; Assert(Convert.ToHexString(SHA256.HashData(input)).Equals("8ee86b7abd13390d06f251de998fb578e149cc42e7ea9114212ff6af4c956828", StringComparison.OrdinalIgnoreCase)); });
Test("Pinned dictionary: exactly 17,000 unique nonzero entries", () => Assert(catalog.Count == 17000 && catalog.Select(f => f.Id).Distinct().Count() == 17000 && catalog.All(f => f.Id > 0)));
Test("Search by ID", () => Assert(Catalog.Search(catalog, "37634385").Single().Name == "TIFE"));
Test("Search is case-insensitive", () => Assert(Catalog.Search(catalog, "tife").Any(f => f.Id == 37634385)));
Test("Unknown search has no invented results", () => Assert(!Catalog.Search(catalog, "this-feature-does-not-exist").Any()));
Test("Malformed dictionary refused", () => Throws<InvalidDataException>(() => Catalog.Parse("hello,zero")));
Test("Zero ID refused", () => Throws<InvalidDataException>(() => Catalog.Parse("zero,0")));
Test("Duplicate ID refused", () => Throws<InvalidDataException>(() => Catalog.Parse("a,1\nb,1")));
Test("Unknown flags have no fabricated illustration", () => Assert(catalog.Where(f => f.Illustrated).Count() == 5 && !new Feature(1, "StartMenuMagic").Illustrated));
var change = new Change(42, "Sample", Snapshot.Default, new(true, OverrideState.Enabled));
Test("Apply verifies and preserves unrelated feature", () => { var store = new FakeStore(); store.Values[99] = new(true, OverrideState.Disabled); var result = ChangeEngine.Apply(store, [change]); Assert(result.Single().Applied && store.Read(42) == change.After && store.Read(99).State == OverrideState.Disabled); });
Test("Default is not disabled", () => Assert(Snapshot.Default != new Snapshot(true, OverrideState.Disabled)));
Test("Existing explicit default differs from absent key", () => Assert(Snapshot.Default != new Snapshot(true, OverrideState.Default)));
Test("Undo restores prior snapshot", () => { var store = new FakeStore(); ChangeEngine.Apply(store, [change]); ChangeEngine.Apply(store, [ChangeEngine.Undo(change, store.Read(42))]); Assert(store.Read(42) == Snapshot.Default); });
Test("Undo rejects external changes", () => Throws<InvalidOperationException>(() => ChangeEngine.Undo(change, new(true, OverrideState.Disabled))));
Test("Preflight conflict makes zero writes", () => { var store = new FakeStore(); store.Values[43] = change.After; Throws<InvalidOperationException>(() => ChangeEngine.Apply(store, [change, change with { Id = 43 }])); Assert(store.Writes == 0); });
Test("Race between preflight and write is blocked", () => { var store = new FakeStore { ChangeOnSecondRead = true }; var result = ChangeEngine.Apply(store, [change]); Assert(!result[0].Applied && store.Writes == 0); });
Test("Partial failure stops remaining batch", () => { var store = new FakeStore { FailOn = 43 }; var result = ChangeEngine.Apply(store, [change, change with { Id = 43 }, change with { Id = 44 }]); Assert(result.Count == 2 && result[0].Applied && !result[1].Applied && store.Read(44) == Snapshot.Default); });
Test("Read-back mismatch is failure", () => { var store = new FakeStore { IgnoreWrites = true }; Assert(!ChangeEngine.Apply(store, [change])[0].Applied); });
Test("Duplicate mutations refused", () => Throws<InvalidDataException>(() => ChangeEngine.Validate([change, change])));
Test("Empty batch refused", () => Throws<InvalidDataException>(() => ChangeEngine.Validate([])));
Test("Overlarge batch refused", () => Throws<InvalidDataException>(() => ChangeEngine.Validate(Enumerable.Range(1, 101).Select(i => change with { Id = (uint)i }).ToArray())));
Test("Invalid enum refused", () => Throws<InvalidDataException>(() => ChangeEngine.Validate([change with { After = new(true, (OverrideState)9) }])));
Test("Invalid absent state refused", () => Throws<InvalidDataException>(() => ChangeEngine.Validate([change with { Before = new(false, OverrideState.Enabled) }])));
Test("Receipt round trip preserves scope and snapshots", () => { var receipt = new Receipt(Guid.NewGuid(), DateTimeOffset.UtcNow, "test", [change]); var restored = JsonSerializer.Deserialize<Receipt>(JsonSerializer.Serialize(receipt)); Assert(restored!.Changes.Single() == change && restored.Results is null); });
Test("Returning to current state removes an older queued enable", () => { var queue = new List<Change>(); ReviewQueue.Stage(queue, change); ReviewQueue.Stage(queue, change with { After = Snapshot.Default }); Assert(queue.Count == 0); });
Test("Canceling one queued item preserves other IDs", () => { var queue = new List<Change> { change, change with { Id = 43 } }; ReviewQueue.Stage(queue, change with { After = Snapshot.Default }); Assert(queue.Count == 1 && queue[0].Id == 43); });
Test("Installed version comes from build metadata", () => Assert(BuildInfo.Version.ToString() == BuildInfo.VersionText && BuildInfo.Version > new Version(0,1,0)));
var bytes = Encoding.UTF8.GetBytes("test package bytes");
var hash = Convert.ToHexString(SHA256.HashData(bytes));
var asset = new ReleaseAsset("ViVeUI-win-x64.exe", new Uri("https://github.com/BlakeLiAFK/ViVeUI/releases/download/v1.0.0/ViVeUI-win-x64.exe"), hash, bytes.Length);
string Json(string? digest = null, string version = "v1.0.0", bool prerelease = false) => JsonSerializer.Serialize(new { draft = false, prerelease, tag_name = version, html_url = "https://github.com/BlakeLiAFK/ViVeUI/releases/tag/" + version, assets = new[] { new { name = asset.Name, browser_download_url = asset.Url.ToString(), digest = digest ?? "sha256:" + hash, size = bytes.Length } } });
Test("Release selects matching architecture and trusted digest", () => Assert(UpdateService.Parse(Json(), "x64").Asset.Sha256 == hash));
Test("Missing digest refused", () => Throws<InvalidDataException>(() => UpdateService.Parse(Json(""), "x64")));
Test("Preview releases refused", () => Throws<InvalidDataException>(() => UpdateService.Parse(Json(prerelease: true), "x64")));
Test("HTTP refused", () => Assert(!UpdateService.TrustedDownload(new("http://github.com/BlakeLiAFK/ViVeUI/releases/download/a/b"))));
Test("Lookalike host refused", () => Assert(!UpdateService.TrustedDownload(new("https://github.com.evil.example/BlakeLiAFK/ViVeUI/releases/download/a/b"))));
Test("Different repository refused", () => Assert(!UpdateService.TrustedDownload(new("https://github.com/someone/ViVeUI/releases/download/a/b"))));
Test("Embedded credentials refused", () => Assert(!UpdateService.TrustedDownload(new("https://user@github.com/BlakeLiAFK/ViVeUI/releases/download/a/b"))));
Test("Nonstandard TLS port refused", () => Assert(!UpdateService.TrustedDownload(new("https://github.com:444/BlakeLiAFK/ViVeUI/releases/download/a/b"))));
await AsyncTest("New release becomes available", async () => { using var service = new UpdateService(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Json()) })); Assert(await service.CheckAsync(new(0, 1), "x64") is not null && service.Status == UpdateStatus.Available); });
await AsyncTest("Equal version is current only after successful check", async () => { using var service = new UpdateService(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Json()) })); Assert(await service.CheckAsync(new(1, 0, 0), "x64") is null && service.Status == UpdateStatus.Current); });
await AsyncTest("Offline failure never reports up to date", async () => { using var service = new UpdateService(new Handler(_ => throw new HttpRequestException("offline"))); await ThrowsAsync<HttpRequestException>(() => service.CheckAsync(new(1, 0), "x64")); Assert(service.Status == UpdateStatus.Failed); });
await AsyncTest("No published release is explicit failure", async () => { using var service = new UpdateService(new Handler(_ => new(HttpStatusCode.NotFound))); await ThrowsAsync<InvalidOperationException>(() => service.CheckAsync(new(1, 0), "x64")); Assert(service.Status == UpdateStatus.Failed); });
var folder = Path.Combine(Path.GetTempPath(), "ViVeUI-tests-" + Guid.NewGuid());
try
{
    await AsyncTest("Valid download is atomically published", async () => { using var service = new UpdateService(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })); var result = await service.DownloadAsync(asset, folder); Assert(File.ReadAllBytes(result).SequenceEqual(bytes) && service.Status == UpdateStatus.Ready); });
    await AsyncTest("Digest failure removes partial and preserves good prior package", async () => { using var service = new UpdateService(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })); await ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(asset with { Sha256 = new string('0', 64) }, folder)); Assert(!Directory.GetFiles(folder, "*.partial").Any() && File.ReadAllBytes(Path.Combine(folder, asset.Name)).SequenceEqual(bytes) && service.Status == UpdateStatus.Failed); });
    await AsyncTest("Truncated download refused", async () => { using var service = new UpdateService(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes[..^1]) })); await ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(asset, folder)); });
    await AsyncTest("Oversized download refused", async () => { using var service = new UpdateService(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes.Concat(bytes).ToArray()) })); await ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(asset, folder)); });
    await AsyncTest("Untrusted redirect refused before fetching destination", async () => { int calls = 0; using var service = new UpdateService(new Handler(_ => { calls++; return new(HttpStatusCode.Redirect) { Headers = { Location = new("https://evil.example/package") } }; })); await ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(asset, folder)); Assert(calls == 1); });
    await AsyncTest("Path traversal filename refused", async () => { using var service = new UpdateService(new Handler(_ => throw new Exception("Must not fetch"))); await ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(asset with { Name = "../escape.exe" }, folder)); });
    await AsyncTest("Trusted GitHub asset redirect verified", async () => { int calls = 0; using var service = new UpdateService(new Handler(_ => ++calls == 1 ? new(HttpStatusCode.Redirect) { Headers = { Location = new("https://release-assets.githubusercontent.com/a") } } : new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })); await service.DownloadAsync(asset, folder); Assert(calls == 2); });
    await AsyncTest("Cancellation after writing bytes removes partial and preserves previous package", async () =>
    {
        using var cancel = new CancellationTokenSource();
        using var service = new UpdateService(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
        var observedPartial = false;
        var progress = new InlineProgress(value => { observedPartial = value > 0 && Directory.GetFiles(folder,"*.partial").Any(); cancel.Cancel(); });
        await ThrowsAsync<OperationCanceledException>(() => service.DownloadAsync(asset, folder, progress, cancel.Token));
        Assert(observedPartial && service.Status == UpdateStatus.Canceled && !Directory.GetFiles(folder,"*.partial").Any() && File.ReadAllBytes(Path.Combine(folder,asset.Name)).SequenceEqual(bytes));
    });
    await AsyncTest("Cancellation never leaves ready package", async () => { using var service = new UpdateService(new Handler(_ => throw new OperationCanceledException())); await ThrowsAsync<OperationCanceledException>(() => service.DownloadAsync(asset, folder)); Assert(service.Status == UpdateStatus.Canceled && !Directory.GetFiles(folder, "*.partial").Any()); });
}
finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
// Cross-platform resource and routing checks; these do not claim Windows rendering or native-speaker review.
Test("Exactly sixteen languages with unique native names", () => Assert(Localization.Languages.Count == 16 && Localization.Languages.Select(l => l.Code).Distinct().Count() == 16 && Localization.Languages.Select(l => l.NativeName).Distinct().Count() == 16));
foreach (var language in Localization.Languages)
    Test("Complete embedded localization: " + language.Code, () =>
    {
        var resource = Localization.Resource(language.Code);
        Localization.ValidateResource(language.Code, Localization.English, resource);
        Assert(resource.Count == 289 && resource.Values.All(v => !string.IsNullOrWhiteSpace(v)));
        Assert(resource.Keys.All(k => Localization.Get(language.Code, k) == resource[k]));
        foreach (var key in new[] { "ReviewCountFormat", "ReviewIdsFormat", "QueueCountFormat" })
            foreach (var count in new[] { 0, 1, 2, 100 }) Assert(!Localization.Format(language.Code, key, count).Contains('{'));
        if (language.Code != "en") Assert(resource.Count(pair => pair.Value == Localization.English[pair.Key]) < resource.Count / 4);
        Assert(System.Globalization.CultureInfo.GetCultureInfo(language.CultureName).Name == language.CultureName);
        Assert(language.FontFamily.Contains("Global User Interface"));
    });
Test("Regional and script language matching", () =>
{
    var cases = new Dictionary<string,string> { ["zh"]="zh-Hans", ["zh-CN"]="zh-Hans", ["zh-SG"]="zh-Hans", ["zh-TW"]="zh-Hant", ["zh-HK"]="zh-Hant", ["zh-MO"]="zh-Hant", ["zh-Hant-CN"]="zh-Hant", ["zh-Hans-HK"]="zh-Hans", ["zh-CHT"]="zh-Hant", ["pt-PT"]="pt-BR", ["pt_BR"]="pt-BR", ["es-MX"]="es", ["fr-CA"]="fr", ["ar-EG"]="ar", ["hi-IN"]="hi", ["EN-gb"]="en", ["tr-TR"]="tr", ["nl-NL"]="en", ["../../fr"]="en" };
    foreach (var pair in cases) Assert(Localization.ResolveCode(pair.Key) == pair.Value);
});
Test("System preference stays distinct from resolved language", () =>
{
    Assert(Localization.NormalizeSelection(null) == "system" && Localization.NormalizeSelection("system") == "system");
    Assert(Localization.ResolveSelection("system", "ja-JP") == "ja" && Localization.ResolveSelection("system", "zh-TW") == "zh-Hant");
    Assert(Localization.ResolveSelection("fr", "ja-JP") == "fr" && Localization.ResolveSelection("system", "nl-NL") == "en");
    Assert(Localization.NormalizeSelection("zh") == "zh-Hans" && Localization.NormalizeSelection("es") == "es");
    foreach (var selection in Localization.Languages.Select(l => l.Code).Append("system")) Assert(Localization.NormalizeSelection(JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(selection))) == selection);
});
Test("Arabic alone uses RTL; native labels use correct scripts", () =>
{
    Assert(Localization.Languages.Where(l => l.IsRightToLeft).Single().Code == "ar");
    Assert(Localization.Get("ar", "Language").Any(c => c is >= '\u0600' and <= '\u06FF'));
    Assert(Localization.Get("hi", "Language").Any(c => c is >= '\u0900' and <= '\u097F'));
    Assert(Localization.Get("ja", "Language").Any(c => c is >= '\u3000' and <= '\u9FFF'));
    Assert(Localization.Get("ko", "Language").Any(c => c is >= '\uAC00' and <= '\uD7AF'));
});
Test("Feature IDs and upstream technical identifiers are culture invariant", () =>
{
    var previous = System.Globalization.CultureInfo.CurrentCulture;
    try
    {
        foreach (var lang in Localization.Languages)
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(lang.CultureName);
            Assert(Localization.FeatureId(uint.MaxValue) == "4294967295" && Catalog.Search(catalog, "tife").Single(f => f.Id == 37634385).Name == "TIFE");
        }
    }
    finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
});
Test("Runtime states and error summaries have resource coverage", () =>
{
    foreach (var code in Localization.Languages.Select(l => l.Code))
    {
        foreach (var state in Enum.GetNames<OverrideState>().Concat(Enum.GetNames<UpdateStatus>())) Assert(!string.IsNullOrEmpty(Localization.Get(code, state)));
        foreach (var key in new[] { "FailureIntegrity", "FailureConflict", "FailurePermission", "FailureUnsupported", "FailureStorage", "FailureIpc", "FailureNetwork", "FailureRequest", "FailureNoRelease", "Confirm", "Cancel", "Close" }) Assert(!string.IsNullOrWhiteSpace(Localization.Get(code, key)));
    }
});
Test("Resource validator rejects missing, extra and empty translations", () =>
{
    var source = new Dictionary<string,string> { ["K"]="Text {0}" };
    Throws<InvalidDataException>(() => Localization.ValidateResource("test", source, new Dictionary<string,string>()));
    Throws<InvalidDataException>(() => Localization.ValidateResource("test", source, new Dictionary<string,string> { ["K"]="Text {0}", ["Extra"]="X" }));
    Throws<InvalidDataException>(() => Localization.ValidateResource("test", source, new Dictionary<string,string> { ["K"]=" " }));
});
Test("Resource validator rejects incorrect placeholders and permits reordering", () =>
{
    var source = new Dictionary<string,string> { ["K"]="{0}: {1}" };
    Throws<InvalidDataException>(() => Localization.ValidateResource("test", source, new Dictionary<string,string> { ["K"]="{0}" }));
    Throws<FormatException>(() => Localization.ValidateResource("test", source, new Dictionary<string,string> { ["K"]="{" }));
    Localization.ValidateResource("test", source, new Dictionary<string,string> { ["K"]="{1} — {0}" });
});
Test("Resources reject hidden bidi overrides", () => Throws<InvalidDataException>(() => Localization.ValidateResource("test", new Dictionary<string,string> { ["K"]="Text" }, new Dictionary<string,string> { ["K"]="Text\u202E" })));
Test("Unknown resource keys cannot silently appear as UI text", () => Throws<KeyNotFoundException>(() => Localization.Get("fr", "MisspelledKey")));
Test("Actionable failure categories preserve safety distinctions", () =>
{
    Assert(Localization.ErrorKey(new UnauthorizedAccessException()) == "FailurePermission");
    Assert(Localization.ErrorKey(new OperationCanceledException()) == "Canceled");
    Assert(Localization.ErrorKey(new InvalidDataException("Package verification failed")) == "FailureIntegrity");
    Assert(Localization.ErrorKey(new InvalidOperationException("Current state changed")) == "FailureConflict");
    Assert(Localization.ErrorKey(new IOException("Worker exited before connecting")) == "FailureIpc");
    Assert(Localization.ErrorKey(new HttpRequestException()) == "FailureNetwork");
    Assert(Localization.ErrorKey(new PlatformNotSupportedException()) == "FailureUnsupported");
    Assert(Localization.ErrorKey(new InvalidOperationException("No published release is available yet")) == "FailureNoRelease");
});
Test("Partial apply preserves failed and unattempted queue items", () =>
{
    var store = new FakeStore { FailOn = 2 }; var queue = new List<Change> { new(1,"one",Snapshot.Default,new(true,OverrideState.Enabled)),new(2,"two",Snapshot.Default,new(true,OverrideState.Enabled)),new(3,"three",Snapshot.Default,new(true,OverrideState.Enabled)) };
    var submitted=queue.ToArray();var results=ChangeEngine.Apply(store,submitted);var remaining=ReviewOperations.Complete(queue,submitted,results);
    Assert(queue.Select(c=>c.Id).SequenceEqual(new uint[]{2,3}) && remaining.Count==2 && store.Writes==2);
    store.FailOn=0;var retried=ChangeEngine.Apply(store,queue.ToArray());ReviewOperations.Complete(queue,queue.ToArray(),retried);Assert(queue.Count==0 && store.Writes==4);
});
Test("Unverified responses cannot remove queued changes", () =>
{
    var change=new Change(1,"one",Snapshot.Default,new(true,OverrideState.Enabled));var queue=new List<Change>{change};
    Throws<InvalidDataException>(()=>ReviewOperations.Complete(queue,[change],[new(change with {Id=2},true,null)]));Assert(queue.Count==1);
    Throws<InvalidDataException>(()=>ReviewOperations.Complete(queue,[change],[new(change,true,null),new(change,true,null)]));Assert(queue.Count==1);
    ReviewOperations.Complete(queue,[change],null);Assert(queue.Count==1);
});
Test("Completion preserves a separately edited queue item", () =>
{
    var change=new Change(1,"one",Snapshot.Default,new(true,OverrideState.Enabled));var changed=change with {After=new(true,OverrideState.Disabled)};var queue=new List<Change>{changed};
    ReviewOperations.Complete(queue,[change],[new(change,true,null)]);Assert(queue.Single()==changed);
});
Test("Restore merge preserves unrelated queued changes", () =>
{
    var a=new Change(1,"one",Snapshot.Default,new(true,OverrideState.Enabled));var b=new Change(2,"two",new(true,OverrideState.Enabled),Snapshot.Default);var queue=new List<Change>{a};ReviewOperations.Merge(queue,[b]);Assert(queue.SequenceEqual(new[]{a,b}));
    ReviewOperations.Merge(queue,[b]);Assert(queue.Count==2);
});
Test("Restore merge conflicts are atomic", () =>
{
    var a=new Change(1,"one",Snapshot.Default,new(true,OverrideState.Enabled));var queue=new List<Change>{a};
    Throws<InvalidOperationException>(()=>ReviewOperations.Merge(queue,[new(2,"two",Snapshot.Default,new(true,OverrideState.Enabled)),a with {After=new(true,OverrideState.Disabled)}]));Assert(queue.Single()==a);
});
Test("Restore merge validates capacity before additions", () =>
{
    var queue=Enumerable.Range(1,100).Select(i=>new Change((uint)i,"entry",Snapshot.Default,new(true,OverrideState.Enabled))).ToList();
    Throws<InvalidOperationException>(()=>ReviewOperations.Merge(queue,[new(101,"extra",Snapshot.Default,new(true,OverrideState.Enabled))]));Assert(queue.Count==100);
});
Test("Localized feature titles participate in full catalog search", () =>
{
    var editorial=new Dictionary<uint,string>{{37634385,"文件资源管理器标签页 Explorer tabs"}};
    Assert(Catalog.Search(Catalog.Load(),"标签页","All",editorial).Single().Id==37634385);
    Assert(Catalog.Search(Catalog.Load(),"Explorer tabs","All",editorial).Single().Id==37634385);
});
Test("Historical feature references cannot enable or disable overrides", () =>
{
    var historicalIds = CuratedCatalog.All.Where(entry => entry.Kind == CuratedKind.Historical).SelectMany(entry => entry.FeatureIds).Distinct().ToArray();
    Assert(historicalIds.Length > 0);
    foreach (var id in historicalIds)
    {
        Assert(CuratedCatalog.IsHistoricalReference(id));
        Throws<InvalidDataException>(() => CuratedCatalog.ValidateOverrideMutation(id, OverrideState.Enabled));
        Throws<InvalidDataException>(() => CuratedCatalog.ValidateOverrideMutation(id, OverrideState.Disabled));
        CuratedCatalog.ValidateOverrideMutation(id, OverrideState.Default);
    }
    const uint unknown = 4294967201;
    Assert(!CuratedCatalog.IsHistoricalReference(unknown));
    foreach (var state in Enum.GetValues<OverrideState>()) CuratedCatalog.ValidateOverrideMutation(unknown, state);
});
Test("Scale persistence values are normalized safely", () =>
{
    Assert(ViewScale.Normalize(1.25)==1.25 && ViewScale.Normalize(1.35)==1.35);
    foreach(var value in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,0,-1,10})Assert(ViewScale.Normalize(value)==1);
    Assert(ViewScale.Normalize(1.249)==1.25);
});
Test("Historical recovery removes overrides rather than writing explicit defaults", () =>
{
    var id = CuratedCatalog.All.First(e => e.Kind == CuratedKind.Historical).FeatureIds[0];
    CuratedCatalog.ValidateOverrideMutation(id, Snapshot.Default);
    Throws<InvalidDataException>(() => CuratedCatalog.ValidateOverrideMutation(id, new Snapshot(true, OverrideState.Default)));
});
passed += CuratedTests.Run();
passed += await ImmediateToggleTests.RunAsync();
passed += UnifiedCatalogTests.Run();
passed += await ManualIdInspectorTests.RunAsync();
Console.WriteLine($"{passed} tests passed. No Windows settings were accessed or modified.");
class InlineProgress(Action<double> report) : IProgress<double> { public void Report(double value) => report(value); }
class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(handle(request)); }
class FakeStore : IFeatureStore
{
    public Dictionary<uint, Snapshot> Values = []; public int Writes; public uint FailOn; public bool IgnoreWrites, ChangeOnSecondRead; int reads;
    public Snapshot Read(uint id) { if (ChangeOnSecondRead && ++reads == 2) Values[id] = new(true, OverrideState.Disabled); return Values.GetValueOrDefault(id, Snapshot.Default); }
    public void Write(uint id, Snapshot state) { Writes++; if (id == FailOn) throw new IOException("Injected write failure"); if (!IgnoreWrites) Values[id] = state; }
}
