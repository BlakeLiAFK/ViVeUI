using System.Collections.Frozen;
using System.Globalization;

namespace ViVeUI.Core;

public sealed record UnifiedCatalogItem(CuratedEntry? Entry, Feature? Feature)
{
    public string Id => Entry?.Id ?? Feature!.Id.ToString(CultureInfo.InvariantCulture);
    // Unknown describes the lack of a reviewed user-facing purpose, never OS state.
    public bool IsUnknown => Entry is null;
    public bool IsHistorical => Entry?.Kind == CuratedKind.Historical;
}

public sealed record UnifiedCatalogResult(IReadOnlyList<UnifiedCatalogItem> Items,
    int KnownCount, int HistoricalCount, int UnknownCount, int HiddenUnknownCount)
{
    // Empty browsing keeps the hidden count but does not advertise a search match.
    public bool HiddenUnknownMatch { get; init; }
}

public static class UnifiedCatalog
{
    static readonly Lazy<FrozenSet<uint>> RepresentedIds = new(() =>
        CuratedCatalog.All.SelectMany(entry => entry.FeatureIds).ToFrozenSet());

    /// <summary>
    /// Returns curated tasks in file order, followed by unknown-purpose raw features
    /// in numeric ID order. Counts describe search matches before visibility filters.
    /// A technical dictionary name alone does not establish a reviewed user purpose.
    /// Raw input may include discovered IDs, but arbitrary numeric queries never
    /// synthesize a feature or bypass an unchecked unknown-purpose filter.
    /// </summary>
    public static UnifiedCatalogResult Query(IEnumerable<Feature> raw, string query, string language,
        bool showKnown = true, bool showHistorical = true, bool showUnknown = false)
    {
        ArgumentNullException.ThrowIfNull(raw);
        query = (query ?? "").Trim();
        // Prefer the first supplied name when dictionary and discovery overlap.
        var distinctRaw = raw.Where(feature => feature.Id != 0).DistinctBy(feature => feature.Id).ToArray();
        var matchingAliases = query.Length == 0 ? [] : distinctRaw
            .Where(feature => RepresentedIds.Value.Contains(feature.Id) && feature.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(feature => feature.Id).ToHashSet();
        var matchingEntries = CuratedCatalog.Search(query, language).Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var items = new List<UnifiedCatalogItem>();
        int knownCount = 0, historicalCount = 0;
        foreach (var entry in CuratedCatalog.All.Where(entry => matchingEntries.Contains(entry.Id) || entry.FeatureIds.Any(matchingAliases.Contains)))
        {
            if (entry.Kind == CuratedKind.Historical)
            {
                historicalCount++;
                if (showHistorical) items.Add(new(entry, null));
            }
            else
            {
                knownCount++;
                if (showKnown) items.Add(new(entry, null));
            }
        }
        var unknown = distinctRaw.Where(feature => !RepresentedIds.Value.Contains(feature.Id))
            .Where(feature => query.Length == 0 || feature.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                feature.Id.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal))
            .ToArray();
        if (showUnknown)
            items.AddRange(unknown.OrderBy(feature => feature.Id).Select(feature => new UnifiedCatalogItem(null, feature)));
        return new(items.AsReadOnly(), knownCount, historicalCount, unknown.Length, showUnknown ? 0 : unknown.Length)
        { HiddenUnknownMatch = query.Length > 0 && !showUnknown && unknown.Length > 0 };
    }
}
