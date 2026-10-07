using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ViVeUI.Core;
public sealed record ReleaseAsset(string Name, Uri Url, string Sha256, long Size);
public sealed record AppRelease(Version Version, Uri Page, ReleaseAsset Asset);
public enum UpdateStatus { Idle, Checking, Current, Available, Downloading, Ready, Failed }
public sealed class UpdateService : IDisposable
{
    public const string Repository = "BlakeLiAFK/ViVeUI";
    public static readonly Uri Endpoint = new($"https://api.github.com/repos/{Repository}/releases/latest");
    readonly HttpClient client;
    public UpdateStatus Status { get; private set; }
    public UpdateService(HttpMessageHandler? handler = null)
    {
        client = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ViVeUI", "0.1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }
    public static bool TrustedDownload(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) &&
        ((uri.Host == "github.com" && uri.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.Ordinal)) || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com");
    public static AppRelease Parse(string json, string architecture)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException("Only stable published releases are supported.");
        var version = Version.Parse(root.GetProperty("tag_name").GetString()!.TrimStart('v'));
        var page = new Uri(root.GetProperty("html_url").GetString()!);
        if (page.Scheme != "https" || page.Host != "github.com" || !page.AbsolutePath.StartsWith($"/{Repository}/releases/tag/", StringComparison.Ordinal)) throw new InvalidDataException("Untrusted release source.");
        var name = $"ViVeUI-win-{architecture}.zip";
        var asset = root.GetProperty("assets").EnumerateArray().Single(a => a.GetProperty("name").GetString() == name);
        var uri = new Uri(asset.GetProperty("browser_download_url").GetString()!);
        var digest = asset.GetProperty("digest").GetString() ?? "";
        var size = asset.GetProperty("size").GetInt64();
        if (!TrustedDownload(uri) || uri.Host != "github.com" || !Regex.IsMatch(digest, "^sha256:[0-9a-fA-F]{64}$") || size is <= 0 or > 250_000_000) throw new InvalidDataException("Release has no valid trusted SHA-256 digest or size.");
        return new(version, page, new(name, uri, digest[7..], size));
    }
    public async Task<AppRelease?> CheckAsync(Version current, string arch, CancellationToken cancellationToken = default)
    {
        Status = UpdateStatus.Checking;
        try
        {
            using var response = await client.GetAsync(Endpoint, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("No published release is available yet.");
            response.EnsureSuccessStatusCode();
            var release = Parse(await response.Content.ReadAsStringAsync(cancellationToken), arch);
            Status = release.Version > current ? UpdateStatus.Available : UpdateStatus.Current;
            return Status == UpdateStatus.Available ? release : null;
        }
        catch { Status = UpdateStatus.Failed; throw; }
    }
    public async Task<string> DownloadAsync(ReleaseAsset asset, string folder, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Status = UpdateStatus.Downloading;
        string? temporary = null;
        try
        {
            if (!TrustedDownload(asset.Url) || !Regex.IsMatch(asset.Sha256, "^[0-9a-fA-F]{64}$") || asset.Size is <= 0 or > 250_000_000 || Path.GetFileName(asset.Name) != asset.Name || !Regex.IsMatch(asset.Name, "^ViVeUI-win-(x64|arm64)\\.zip$")) throw new InvalidDataException("Untrusted package.");
            Directory.CreateDirectory(folder);
            temporary = Path.Combine(folder, Guid.NewGuid() + ".partial");
            var uri = asset.Url;
            HttpResponseMessage? response = null;
            for (var i = 0; i < 5; i++)
            {
                if (!TrustedDownload(uri)) throw new InvalidDataException("Untrusted download redirect.");
                response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    var location = response.Headers.Location ?? throw new InvalidDataException("Missing redirect.");
                    uri = location.IsAbsoluteUri ? location : new Uri(uri, location); response.Dispose(); response = null; continue;
                }
                break;
            }
            using (response)
            {
                if (response is null) throw new InvalidDataException("Too many download redirects.");
                response.EnsureSuccessStatusCode();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920]; long total = 0; int count;
                    while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        total += count; if (total > asset.Size) throw new InvalidDataException("Package exceeds declared size.");
                        hash.AppendData(buffer, 0, count); await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken); progress?.Report((double)total / asset.Size);
                    }
                    if (total != asset.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package verification failed. Nothing was installed.");
                }
            }
            var target = Path.Combine(folder, asset.Name); File.Move(temporary, target, true); Status = UpdateStatus.Ready; return target;
        }
        catch { Status = UpdateStatus.Failed; if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); throw; }
    }
    public void Dispose() => client.Dispose();
}
