using System.Reflection;
namespace ViVeUI.Core;
public static class BuildInfo
{
    public static string VersionText => typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    public static Version Version => Version.Parse(VersionText);
}
public static class ReviewQueue
{
    public static void Stage(IList<Change> queue, Change change)
    {
        ChangeEngine.Validate([change]);
        var existing = queue.FirstOrDefault(c => c.Id == change.Id);
        if (existing is not null) queue.Remove(existing);
        // Returning to the observed state must cancel an earlier queued override.
        if (change.Before == change.After) return;
        if (queue.Count >= 100) throw new InvalidOperationException("Review is limited to 100 changes.");
        queue.Add(change);
    }
}
