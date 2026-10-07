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
var bytes = Encoding.UTF8.GetBytes("test package bytes");
var hash = Convert.ToHexString(SHA256.HashData(bytes));
var asset = new ReleaseAsset("ViVeUI-win-x64.zip", new Uri("https://github.com/BlakeLiAFK/ViVeUI/releases/download/v1.0.0/ViVeUI-win-x64.zip"), hash, bytes.Length);
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
    await AsyncTest("Path traversal filename refused", async () => { using var service = new UpdateService(new Handler(_ => throw new Exception("Must not fetch"))); await ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(asset with { Name = "../escape.zip" }, folder)); });
    await AsyncTest("Trusted GitHub asset redirect verified", async () => { int calls = 0; using var service = new UpdateService(new Handler(_ => ++calls == 1 ? new(HttpStatusCode.Redirect) { Headers = { Location = new("https://release-assets.githubusercontent.com/a") } } : new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })); await service.DownloadAsync(asset, folder); Assert(calls == 2); });
    await AsyncTest("Cancellation never leaves ready package", async () => { using var service = new UpdateService(new Handler(_ => throw new OperationCanceledException())); await ThrowsAsync<OperationCanceledException>(() => service.DownloadAsync(asset, folder)); Assert(service.Status == UpdateStatus.Failed && !Directory.GetFiles(folder, "*.partial").Any()); });
}
finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
Console.WriteLine($"{passed} tests passed. No Windows settings were accessed or modified.");
class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(handle(request)); }
class FakeStore : IFeatureStore
{
    public Dictionary<uint, Snapshot> Values = []; public int Writes; public uint FailOn; public bool IgnoreWrites, ChangeOnSecondRead; int reads;
    public Snapshot Read(uint id) { if (ChangeOnSecondRead && ++reads == 2) Values[id] = new(true, OverrideState.Disabled); return Values.GetValueOrDefault(id, Snapshot.Default); }
    public void Write(uint id, Snapshot state) { Writes++; if (id == FailOn) throw new IOException("Injected write failure"); if (!IgnoreWrites) Values[id] = state; }
}
