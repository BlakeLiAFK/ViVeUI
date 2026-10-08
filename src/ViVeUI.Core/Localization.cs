using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ViVeUI.Core;

public sealed record LanguageDefinition(string Code, string NativeName, string CultureName, string FontFamily, bool IsRightToLeft = false);

public static class Localization
{
    // Capture the user's UI culture before a selected language changes thread culture.
    public static readonly string SystemCulture = CultureInfo.CurrentUICulture.Name;
    public static IReadOnlyList<LanguageDefinition> Languages { get; } = Array.AsReadOnly(new[]
    {
        new LanguageDefinition("en", "English", "en-US", "Segoe UI, Global User Interface"),
        new("zh-Hans", "简体中文", "zh-CN", "Microsoft YaHei UI, Microsoft YaHei, Global User Interface"),
        new("zh-Hant", "繁體中文", "zh-TW", "Microsoft JhengHei UI, Microsoft JhengHei, Global User Interface"),
        new("ja", "日本語", "ja-JP", "Yu Gothic UI, Meiryo, Global User Interface"),
        new("ko", "한국어", "ko-KR", "Malgun Gothic, Global User Interface"),
        new("fr", "Français", "fr-FR", "Segoe UI, Global User Interface"),
        new("de", "Deutsch", "de-DE", "Segoe UI, Global User Interface"),
        new("es", "Español", "es-ES", "Segoe UI, Global User Interface"),
        new("pt-BR", "Português (Brasil)", "pt-BR", "Segoe UI, Global User Interface"),
        new("it", "Italiano", "it-IT", "Segoe UI, Global User Interface"),
        new("ru", "Русский", "ru-RU", "Segoe UI, Global User Interface"),
        new("ar", "العربية", "ar-SA", "Segoe UI, Tahoma, Global User Interface", true),
        new("hi", "हिन्दी", "hi-IN", "Nirmala UI, Mangal, Global User Interface"),
        new("id", "Bahasa Indonesia", "id-ID", "Segoe UI, Global User Interface"),
        new("tr", "Türkçe", "tr-TR", "Segoe UI, Global User Interface"),
        new("vi", "Tiếng Việt", "vi-VN", "Segoe UI, Global User Interface")
    });
    static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Resources = Load();
    public static IReadOnlyDictionary<string, string> English => Resources["en"];
    public static string NormalizeSelection(string? selection) => string.IsNullOrWhiteSpace(selection) || selection.Equals("system", StringComparison.OrdinalIgnoreCase) ? "system" : ResolveCode(selection);
    public static string ResolveSelection(string? selection, string? systemCulture = null) => NormalizeSelection(selection) is var preferred && preferred != "system" ? preferred : ResolveCode(systemCulture ?? SystemCulture);
    public static string ResolveCode(string? culture)
    {
        var tag = (culture ?? "").Replace('_', '-').ToLowerInvariant();
        var parts = tag.Split('-');
        if (parts[0] == "zh")
        {
            if (parts.Contains("hant") || parts.Contains("cht")) return "zh-Hant";
            if (parts.Contains("hans") || parts.Contains("chs")) return "zh-Hans";
            return parts.Any(p => p is "tw" or "hk" or "mo") ? "zh-Hant" : "zh-Hans";
        }
        if (parts[0] == "pt") return "pt-BR";
        return Languages.FirstOrDefault(l => l.Code.Equals(parts[0], StringComparison.OrdinalIgnoreCase))?.Code ?? "en";
    }
    public static LanguageDefinition Definition(string code) => Languages.Single(l => l.Code == ResolveCode(code));
    public static IReadOnlyDictionary<string, string> Resource(string code) => Resources[ResolveCode(code)];
    public static string Get(string code, string key) => Resource(code).TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Missing localization key: {key}");
    public static string Format(string code, string key, params object[] values) => string.Format(CultureInfo.GetCultureInfo(Definition(code).CultureName), Get(code, key), values);
    public static string FeatureId(uint id) => id.ToString(CultureInfo.InvariantCulture);

    static FrozenDictionary<string, FrozenDictionary<string, string>> Load()
    {
        var result = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            using var stream = typeof(Localization).Assembly.GetManifestResourceStream($"ViVeUI.Core.Localization.{language.Code}.json") ?? throw new InvalidDataException($"Missing resource: {language.Code}");
            using var document = JsonDocument.Parse(stream);
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String || !entries.TryAdd(property.Name, property.Value.GetString()!)) throw new InvalidDataException($"Invalid or duplicate resource: {language.Code}/{property.Name}");
            }
            result.Add(language.Code, entries.ToFrozenDictionary(StringComparer.Ordinal));
        }
        foreach (var entry in result) ValidateResource(entry.Key, result["en"], entry.Value);
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }
    public static void ValidateResource(string code, IReadOnlyDictionary<string,string> source, IReadOnlyDictionary<string,string> translated)
    {
        if (!source.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(translated.Keys)) throw new InvalidDataException($"Resource keys differ: {code}");
        foreach (var (key, value) in translated)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains('\uFFFD') || value.Any(c => c is >= '\u202A' and <= '\u202E' or >= '\u2066' and <= '\u2069')) throw new InvalidDataException($"Invalid resource text: {code}/{key}");
            var expected = CompositeFormat.Parse(source[key]); var actual = CompositeFormat.Parse(value);
            static string Slots(string text) => string.Join(",", Regex.Matches(text, @"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})").Select(m => m.Groups[1].Value).Order());
            if (expected.MinimumArgumentCount != actual.MinimumArgumentCount || Slots(source[key]) != Slots(value)) throw new InvalidDataException($"Resource placeholders differ: {code}/{key}");
        }
    }
    // Localized, actionable summaries; exact upstream/OS diagnostics remain available separately.
    public static string ErrorKey(Exception error)
    {
        if (error is OperationCanceledException) return "Canceled";
        if (error is System.ComponentModel.Win32Exception win && win.NativeErrorCode is 5 or 1223 || error is UnauthorizedAccessException) return "FailurePermission";
        if (error is HttpRequestException) return "FailureNetwork";
        if (error is PlatformNotSupportedException) return "FailureUnsupported";
        var message = error.Message.ToLowerInvariant();
        if (message.Contains("no published release")) return "FailureNoRelease";
        if (message.Contains("conflict") || message.Contains("changed") || message.Contains("read-back")) return "FailureConflict";
        if (message.Contains("worker") || message.Contains("ipc") || message.Contains("pipe")) return "FailureIpc";
        if (message.Contains("package") || message.Contains("digest") || message.Contains("redirect") || message.Contains("release") || message.Contains("download")) return "FailureIntegrity";
        if (message.Contains("unsupported") || message.Contains("advanced") || message.Contains("variant")) return "FailureUnsupported";
        if (error is InvalidDataException || message.Contains("review is limited") || message.Contains("batch")) return "FailureRequest";
        if (error is IOException) return "FailureStorage";
        return "Error";
    }
}
