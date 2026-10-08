using System.Reflection;
using System.Security.Cryptography;

namespace ViVeUI.Core;
public enum OverrideState { Default, Disabled, Enabled }
public sealed record Snapshot(bool Exists, OverrideState State)
{
    public static Snapshot Default => new(false, OverrideState.Default);
    public string Label => Exists ? State.ToString() : "Windows default";
}
public sealed record Feature(uint Id, string Name)
{
    public string Category => Id switch { 37634385 or 36354489 => "Explorer", 34300186 => "Widgets", 39420424 or 40430431 => "System", _ => "Catalog" };
    public bool Illustrated => Category != "Catalog";
}
public static class Catalog
{
    public const string Commit = "3f8c6a3425983412da1e8b26cd757c3aa17b3f25";
    public const string Source = "https://github.com/thebookisclosed/ViVe/blob/" + Commit + "/Extra/FeatureDictionary.pfs";
    public static IReadOnlyList<Feature> Load()
    {
        using var stream = typeof(Catalog).Assembly.GetManifestResourceStream("ViVeUI.Core.FeatureDictionary.pfs") ?? throw new InvalidDataException("Embedded catalog missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
    public static IReadOnlyList<Feature> Parse(string text)
    {
        var result = new Dictionary<uint, Feature>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = line.LastIndexOf(',');
            if (index < 1 || !uint.TryParse(line[(index + 1)..], out var id) || id == 0)
                throw new InvalidDataException("Invalid feature dictionary entry.");
            if (!result.TryAdd(id, new(id, line[..index]))) throw new InvalidDataException("Duplicate feature ID.");
        }
        return result.Values.OrderByDescending(x => x.Illustrated).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static IEnumerable<Feature> Search(IEnumerable<Feature> features, string query, string category = "All", IReadOnlyDictionary<uint,string>? editorial = null)
    {
        query = (query ?? "").Trim();
        return features.Where(f => (category == "All" || f.Category == category) && (f.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || f.Id.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal) || (editorial is not null && editorial.TryGetValue(f.Id, out var description) && description.Contains(query, StringComparison.OrdinalIgnoreCase))));
    }
}
public sealed record Change(uint Id, string Name, Snapshot Before, Snapshot After);
public sealed record ChangeResult(Change Change, bool Applied, string? Error);
public sealed record Receipt(Guid Id, DateTimeOffset At, string Build, List<Change> Changes, List<ChangeResult>? Results = null);
public interface IFeatureStore { Snapshot Read(uint id); void Write(uint id, Snapshot state); }
public static class ChangeEngine
{
    public static void Validate(IReadOnlyList<Change> changes)
    {
        if (changes.Count is < 1 or > 100 || changes.Select(c => c.Id).Distinct().Count() != changes.Count)
            throw new InvalidDataException("A batch must contain 1–100 unique IDs.");
        foreach (var c in changes)
            if (c.Id == 0 || c.Name.Length > 256 || !Enum.IsDefined(c.Before.State) || !Enum.IsDefined(c.After.State) || (!c.Before.Exists && c.Before.State != OverrideState.Default) || (!c.After.Exists && c.After.State != OverrideState.Default))
                throw new InvalidDataException("Invalid feature change.");
    }
    public static List<ChangeResult> Apply(IFeatureStore store, IReadOnlyList<Change> changes)
    {
        Validate(changes);
        // Preflight the entire batch before any mutation, then check each item again.
        foreach (var c in changes) if (store.Read(c.Id) != c.Before) throw new InvalidOperationException($"Conflict: {c.Id}. Refresh and review again.");
        var results = new List<ChangeResult>();
        foreach (var c in changes)
        {
            try
            {
                if (store.Read(c.Id) != c.Before) throw new InvalidOperationException("Configuration changed since review.");
                store.Write(c.Id, c.After);
                if (store.Read(c.Id) != c.After) throw new IOException("Read-back verification failed. Inspect this feature before retrying.");
                results.Add(new(c, true, null));
            }
            catch (Exception e) { results.Add(new(c, false, e.Message)); break; }
        }
        return results;
    }
    public static Change Undo(Change change, Snapshot current)
    {
        if (current != change.After) throw new InvalidOperationException("Undo conflict: current override differs from this change.");
        return new(change.Id, change.Name, current, change.Before);
    }
}
