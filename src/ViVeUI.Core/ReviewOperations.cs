namespace ViVeUI.Core;
public static class ReviewOperations
{
    // Only verified successes from this exact submitted batch may leave the review queue.
    public static IReadOnlyList<Change> Complete(IList<Change> queue, IReadOnlyList<Change> submitted, IReadOnlyList<ChangeResult>? results)
    {
        ChangeEngine.Validate(submitted);
        var returned = results ?? [];
        if (returned.Select(r => r.Change.Id).Distinct().Count() != returned.Count || returned.Any(r => !submitted.Contains(r.Change)))
            throw new InvalidDataException("Invalid batch response.");
        foreach (var result in returned.Where(r => r.Applied))
        {
            var current = queue.FirstOrDefault(c => c.Id == result.Change.Id);
            if (current == result.Change) queue.Remove(current);
        }
        return submitted.Where(c => !returned.Any(r => r.Change == c && r.Applied)).ToArray();
    }
    // Validate the whole merge before mutation: restoration must not discard unrelated work.
    public static void Merge(IList<Change> queue, IReadOnlyList<Change> incoming)
    {
        ChangeEngine.Validate(incoming);
        foreach (var change in incoming)
            if (queue.FirstOrDefault(c => c.Id == change.Id) is Change old && old != change)
                throw new InvalidOperationException($"Review conflict: {change.Id}. Remove its existing queued change before restoring.");
        var additions = incoming.Where(c => c.Before != c.After && !queue.Contains(c)).ToArray();
        if (queue.Count + additions.Length > 100) throw new InvalidOperationException("Review is limited to 100 changes.");
        foreach (var change in additions) queue.Add(change);
    }
}
public static class ViewScale
{
    public static double Normalize(double value) => double.IsFinite(value) && value >= 1 && value <= 1.35 ? Math.Round(value * 20) / 20 : 1;
}
