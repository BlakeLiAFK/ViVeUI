using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ViVeUI.Core;

public enum CuratedKind { NativeSettings, NativeShortcut, Guide, Historical, FeatureFlag }
public enum CuratedRisk { None, UnsavedWork, Files, Privacy, Power, Accessibility, Experimental, Network, Security }
public enum CuratedRestart { None, App, SignOut, Device, Varies }
public enum CuratedRestore { None, PreviousSetting, CloseView, Backup, Manual }
public sealed record CuratedText(string Title, string Body, string Keywords, string Evidence);
public sealed record CuratedEntry(string Id, CuratedKind Kind, string Category, string Title, string Body,
    string Keywords, IReadOnlyList<string> Sources, string Evidence, IReadOnlyList<uint> FeatureIds,
    string? Destination, CuratedRisk Risk, CuratedRestart Restart, CuratedRestore Restore, string Illustration);

public static class CuratedCatalog
{
    static readonly string[] EntryFields = ["id", "kind", "category", "title", "body", "keywords", "sources", "evidence", "featureIds", "destination", "risk", "restart", "restore", "illustration"];
    static readonly string[] TextFields = ["title", "body", "keywords", "evidence"];
    static readonly FrozenSet<string> Categories = new[] { "ContextMenu", "Explorer", "Taskbar", "Start", "Windows", "Input", "Accessibility", "Appearance", "Notifications", "Performance", "SystemTools", "Privacy" }.ToFrozenSet(StringComparer.Ordinal);
    static readonly FrozenSet<string> Illustrations = new[] { "Context", "Explorer", "Taskbar", "Layout", "Sound", "Settings", "Tabs", "Widgets", "Search" }.ToFrozenSet(StringComparer.Ordinal);
    // Literal destinations from Microsoft's launch-settings reference. Never accept arguments,
    // query strings, arbitrary executables, or URLs supplied by catalog consumers.
    static readonly FrozenSet<string> Destinations = new[]
    {
        "explorer.exe", "ms-settings:", "ms-settings:display", "ms-settings:nightlight", "ms-settings:display-advanced",
        "ms-settings:screenrotation", "ms-settings:quietmomentspresentation", "ms-settings:quietmomentsgame",
        "ms-settings:sound", "ms-settings:apps-volume", "ms-settings:notifications", "ms-settings:quiethours",
        "ms-settings:powersleep", "ms-settings:batterysaver", "ms-settings:batterysaver-usagedetails",
        "ms-settings:energyrecommendations", "ms-settings:storagesense", "ms-settings:storagepolicies",
        "ms-settings:multitasking", "ms-settings:clipboard", "ms-settings:about", "ms-settings:optionalfeatures",
        "ms-settings:developers", "ms-settings:recovery", "ms-settings:activation", "ms-settings:troubleshoot",
        "ms-settings:taskbar", "ms-settings:personalization", "ms-settings:personalization-background",
        "ms-settings:personalization-colors", "ms-settings:colors", "ms-settings:lockscreen", "ms-settings:themes",
        "ms-settings:fonts", "ms-settings:personalization-start", "ms-settings:personalization-touchkeyboard",
        "ms-settings:dateandtime", "ms-settings:regionlanguage", "ms-settings:typing",
        "ms-settings:keyboard", "ms-settings:mousetouchpad", "ms-settings:devices-touchpad", "ms-settings:pen",
        "ms-settings:bluetooth", "ms-settings:connecteddevices", "ms-settings:printers", "ms-settings:autoplay",
        "ms-settings:usb", "ms-settings:wheel", "ms-settings:easeofaccess-display", "ms-settings:easeofaccess-cursor",
        "ms-settings:easeofaccess-mousepointer", "ms-settings:easeofaccess-magnifier", "ms-settings:easeofaccess-colorfilter",
        "ms-settings:easeofaccess-highcontrast", "ms-settings:easeofaccess-narrator", "ms-settings:easeofaccess-audio",
        "ms-settings:easeofaccess-closedcaptioning", "ms-settings:easeofaccess-speechrecognition",
        "ms-settings:easeofaccess-keyboard", "ms-settings:easeofaccess-mouse", "ms-settings:easeofaccess-eyecontrol",
        "ms-settings:privacy", "ms-settings:privacy-general", "ms-settings:privacy-speech", "ms-settings:privacy-speechtyping",
        "ms-settings:privacy-feedback", "ms-settings:privacy-activityhistory", "ms-settings:privacy-location",
        "ms-settings:privacy-webcam", "ms-settings:privacy-microphone", "ms-settings:privacy-notifications",
        "ms-settings:privacy-accountinfo", "ms-settings:privacy-contacts", "ms-settings:privacy-calendar",
        "ms-settings:privacy-phonecalls", "ms-settings:privacy-callhistory", "ms-settings:privacy-email",
        "ms-settings:privacy-tasks", "ms-settings:privacy-messaging", "ms-settings:privacy-radios",
        "ms-settings:privacy-customdevices", "ms-settings:privacy-backgroundapps", "ms-settings:privacy-appdiagnostics",
        "ms-settings:privacy-documents", "ms-settings:privacy-downloadsfolder", "ms-settings:privacy-pictures",
        "ms-settings:privacy-videos", "ms-settings:privacy-broadfilesystemaccess",
        "ms-settings:search-permissions", "ms-settings:search", "ms-settings:windowsupdate",
        "ms-settings:windowsupdate-history", "ms-settings:windowsupdate-options", "ms-settings:delivery-optimization",
        "ms-settings:windowsdefender", "ms-settings:deviceencryption", "ms-settings:network-status",
        "ms-settings:network-wifi", "ms-settings:network-ethernet", "ms-settings:network-vpn",
        "ms-settings:network-proxy", "ms-settings:network-mobilehotspot", "ms-settings:network-airplanemode",
        "ms-settings:network-advancedsettings", "ms-settings:network-wifisettings",
        "ms-settings:appsfeatures", "ms-settings:defaultapps", "ms-settings:startupapps", "ms-settings:maps",
        "ms-settings:videoplayback", "ms-settings:yourinfo", "ms-settings:emailandaccounts", "ms-settings:signinoptions",
        "ms-settings:otherusers", "ms-settings:workplace", "ms-settings:backup", "ms-settings:sync",
        "ms-settings:easeofaccess-visualeffects", "ms-settings:easeofaccess-hearingaids", "ms-settings:keyboard-advanced",
        "ms-settings:personalization-start-places", "ms-settings:personalization-textinput", "ms-settings:personalization-lighting",
        "ms-settings:display-advancedgraphics", "ms-settings:display-advancedgraphics-default", "ms-settings:savelocations",
        "ms-settings:storagerecommendations", "ms-settings:disksandvolumes", "ms-settings:remotedesktop", "ms-settings:presence",
        "ms-settings:regionformatting", "ms-settings:speech", "ms-settings:findmydevice", "ms-settings:camera",
        "ms-settings:devicestyping-hwkbtextsuggestions", "ms-settings:devices-touch", "ms-settings:mobile-devices",
        "ms-settings:privacy-musiclibrary", "ms-settings:privacy-voiceactivation", "ms-settings:privacy-automaticfiledownloads",
        "ms-settings:windowsupdate-activehours", "ms-settings:windowsupdate-optionalupdates", "ms-settings:windowsupdate-restartoptions",
        "ms-settings:quietmomentsscheduled", "ms-settings:signinoptions-dynamiclock", "ms-settings:sound-devices",
        "ms-settings:sound-defaultinputproperties", "ms-settings:sound-defaultoutputproperties", "ms-settings:search-moredetails",
        "ms-settings:gaming-gamebar", "ms-settings:gaming-gamedvr", "ms-settings:gaming-gamemode"
    }.ToFrozenSet(StringComparer.Ordinal);
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    static readonly Lazy<IReadOnlyList<CuratedEntry>> Entries = new(() => Parse(ReadResource("entries.json")));
    static readonly Lazy<FrozenSet<uint>> HistoricalFeatureIds = new(() => All
        .Where(entry => entry.Kind == CuratedKind.Historical)
        .SelectMany(entry => entry.FeatureIds).ToFrozenSet());
    static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, CuratedText>>> Locales = new(() =>
    {
        var result = Localization.Languages.ToDictionary(l => l.Code, l => ParseLocale(ReadResource($"Locales.{l.Code}.json")), StringComparer.Ordinal);
        ValidateLocales(All, result);
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    });
    public static IReadOnlyList<CuratedEntry> All => Entries.Value;
    public static bool IsHistoricalReference(uint id) => HistoricalFeatureIds.Value.Contains(id);
    // Archival evidence does not establish compatibility with the current device.
    // Clearing an override remains available to recover from an earlier change.
    public static void ValidateOverrideMutation(uint id, OverrideState state)
    {
        if (id == 0 || !Enum.IsDefined(state)) throw new InvalidDataException("Invalid override mutation.");
        if (state != OverrideState.Default && IsHistoricalReference(id))
            throw new InvalidDataException($"Historical feature reference is read-only: {id}. Only restoring the Windows default is permitted.");
    }
    public static void ValidateOverrideMutation(uint id, Snapshot target)
    {
        ValidateOverrideMutation(id, target.State);
        if (target.Exists && IsHistoricalReference(id))
            throw new InvalidDataException($"Historical feature reference is read-only: {id}. Recovery must remove the user override.");
    }
    public static CuratedText Text(CuratedEntry entry, string language) => Locales.Value[Localization.ResolveSelection(language)][entry.Id];
    public static IEnumerable<CuratedEntry> Search(string query, string language, string category = "All", string kind = "All")
    {
        query = (query ?? "").Trim();
        foreach (var entry in All)
        {
            if (category != "All" && entry.Category != category || kind != "All" && entry.Kind.ToString() != kind) continue;
            var text = Text(entry, language);
            if (query.Length == 0 || new[] { entry.Id, text.Title, text.Body, text.Keywords }.Any(s => s.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                entry.FeatureIds.Any(id => id.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal))) yield return entry;
        }
    }
    public static bool IsAllowedDestination(string? destination) => destination is null || Destinations.Contains(destination);
    public static bool IsAllowedSource(string? source) => !string.IsNullOrWhiteSpace(source) &&
        Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.UserInfo.Length == 0 && uri.IsDefaultPort;
    public static IReadOnlyList<CuratedEntry> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Catalog must be an array.");
        var result = new List<CuratedEntry>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            ValidateFields(element, EntryFields);
            var entry = element.Deserialize<CuratedEntry>(JsonOptions) ?? throw new InvalidDataException("Empty catalog entry.");
            if (entry.Sources is null || entry.FeatureIds is null) throw new InvalidDataException("Catalog arrays must not be null.");
            result.Add(entry with { Sources = Array.AsReadOnly(entry.Sources.ToArray()), FeatureIds = Array.AsReadOnly(entry.FeatureIds.ToArray()) });
        }
        Validate(result);
        return result.AsReadOnly();
    }
    public static IReadOnlyDictionary<string, CuratedText> ParseLocale(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Locale must be an object.");
        var result = new Dictionary<string, CuratedText>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            ValidateFields(property.Value, TextFields);
            if (!result.TryAdd(property.Name, property.Value.Deserialize<CuratedText>(JsonOptions)!)) throw new InvalidDataException($"Duplicate locale ID: {property.Name}");
        }
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }
    public static void Validate(IReadOnlyList<CuratedEntry> entries)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var titles = new HashSet<string>(StringComparer.Ordinal);
        var bodies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            ValidateText(entry.Id, entry.Title, entry.Body, entry.Keywords, entry.Evidence);
            if (!char.IsAsciiLetterUpper(entry.Id[0]) || entry.Id.Any(c => !char.IsAsciiLetterOrDigit(c)) || !ids.Add(entry.Id)) throw new InvalidDataException($"Invalid or duplicate catalog ID: {entry.Id}");
            if (!titles.Add(Normalize(entry.Title)) || !bodies.Add(Normalize(entry.Body))) throw new InvalidDataException($"Duplicate catalog task: {entry.Id}");
            if (!Categories.Contains(entry.Category) || !Illustrations.Contains(entry.Illustration) || !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.Risk) || !Enum.IsDefined(entry.Restart) || !Enum.IsDefined(entry.Restore)) throw new InvalidDataException($"Invalid catalog metadata: {entry.Id}");
            if (!IsAllowedDestination(entry.Destination) || entry.Kind == CuratedKind.Historical && entry.Destination is not null) throw new InvalidDataException($"Unsafe destination: {entry.Id}");
            // No reviewed current-build verification provider exists in this release.
            if (entry.Kind == CuratedKind.FeatureFlag) throw new InvalidDataException($"Feature flag lacks independent current-build verification: {entry.Id}");
            if (entry.FeatureIds is null || entry.FeatureIds.Any(id => id == 0) || entry.FeatureIds.Distinct().Count() != entry.FeatureIds.Count) throw new InvalidDataException($"Invalid feature IDs: {entry.Id}");
            var sources = new HashSet<string>(StringComparer.Ordinal);
            if (entry.Sources is null || entry.Sources.Count == 0) throw new InvalidDataException($"Missing sources: {entry.Id}");
            foreach (var source in entry.Sources)
            {
                ValidateText(source);
                if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || !uri.IsDefaultPort || !sources.Add(uri.AbsoluteUri)) throw new InvalidDataException($"Invalid or duplicate source: {entry.Id}");
            }
        }
    }
    public static void ValidateLocales(IReadOnlyList<CuratedEntry> entries, IReadOnlyDictionary<string, IReadOnlyDictionary<string, CuratedText>> locales)
    {
        if (!Localization.Languages.Select(l => l.Code).ToHashSet(StringComparer.Ordinal).SetEquals(locales.Keys)) throw new InvalidDataException("Catalog locale set differs from supported languages.");
        var ids = entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (language, texts) in locales)
        {
            if (!ids.SetEquals(texts.Keys)) throw new InvalidDataException($"Catalog locale IDs differ: {language}");
            foreach (var entry in entries)
            {
                var text = texts[entry.Id] ?? throw new InvalidDataException($"Missing text: {language}/{entry.Id}");
                ValidateText(text.Title, text.Body, text.Keywords, text.Evidence);
                if (language != "en" && Normalize(text.Body) == Normalize(entry.Body)) throw new InvalidDataException($"Untranslated catalog body: {language}/{entry.Id}");
            }
        }
    }
    static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit)).ToUpperInvariant();
    static void ValidateText(params string[] values)
    {
        if (values.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(c => c is '\uFFFD' or '\u061C' or '\u200E' or '\u200F' or >= '\u202A' and <= '\u202E' or >= '\u2066' and <= '\u2069' || char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))) throw new InvalidDataException("Blank or unsafe catalog text.");
    }
    static void ValidateFields(JsonElement element, string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Catalog item must be an object.");
        var names = element.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != expected.Length || !names.ToHashSet(StringComparer.Ordinal).SetEquals(expected)) throw new InvalidDataException("Catalog item fields are missing, duplicated, or unknown.");
    }
    static string ReadResource(string name)
    {
        using var stream = typeof(CuratedCatalog).Assembly.GetManifestResourceStream($"ViVeUI.Core.Catalog.{name}") ?? throw new InvalidDataException($"Missing catalog resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
