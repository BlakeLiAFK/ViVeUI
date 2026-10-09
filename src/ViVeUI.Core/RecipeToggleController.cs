using System.Globalization;

namespace ViVeUI.Core;

public sealed record RecipeToggleOutcome(FeatureRecipe Recipe, IReadOnlyList<Change> Changes,
    IReadOnlyList<ChangeResult> Results, IReadOnlyList<ImmediateToggleState> Actual, Exception? Error)
{
    public bool Canceled => Error is OperationCanceledException;
    public bool? State => Actual.Count == Recipe.FeatureIds.Count && Actual.All(item => item.State == true) ? true :
        Actual.Count == Recipe.FeatureIds.Count && Actual.All(item => item.State == false) ? false : null;
    public bool Succeeded => Error is null && Changes.Count == Recipe.FeatureIds.Count &&
        Actual.Count == Changes.Count && Changes.All(change => Actual.Any(actual => actual.Id == change.Id &&
            actual.ReadSucceeded && actual.Snapshot == change.After)) &&
        (Results.Count == 0 && Changes.All(change => change.Before == change.After) ||
         Results.Count == Changes.Count && Results.All(result => result.Applied));
}

/// <summary>
/// One atomic admission gate for one complete recipe operation. The write group is
/// not a transactional OS operation: partial results and all readbacks remain visible.
/// The injected backend owns receipts/elevation and must validate the trusted recipe
/// scope and Before snapshots again on its execution side immediately before writing.
/// </summary>
public sealed class RecipeToggleController(IFeatureStore store,
    Func<IReadOnlyList<Change>, string, CancellationToken, Task<IReadOnlyList<ChangeResult>>> execute)
{
    readonly IFeatureStore store = store ?? throw new ArgumentNullException(nameof(store));
    readonly Func<IReadOnlyList<Change>, string, CancellationToken, Task<IReadOnlyList<ChangeResult>>> execute =
        execute ?? throw new ArgumentNullException(nameof(execute));
    int busy;
    public bool IsBusy => Volatile.Read(ref busy) != 0;

    public IReadOnlyList<ImmediateToggleState> Read(string recipeId) =>
        Array.AsReadOnly(FeatureRecipes.Get(recipeId).FeatureIds.Select(ReadState).ToArray());

    ImmediateToggleState ReadState(uint id)
    {
        try
        {
            var snapshot = store.Read(id);
            if (snapshot is null || !Enum.IsDefined(snapshot.State) || !snapshot.Exists && snapshot.State != OverrideState.Default)
                throw new InvalidDataException("Invalid recipe feature read-back snapshot.");
            return new(id, snapshot, null);
        }
        catch (Exception error) { return new(id, null, error); }
    }

    public async Task<RecipeToggleOutcome> ApplyAsync(string recipeId, Snapshot target, DeviceBuild device,
        IReadOnlySet<uint> observedIds, IReadOnlyDictionary<string, bool?>? prerequisites = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var recipe = FeatureRecipes.Get(recipeId);
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            throw new InvalidOperationException("A recipe operation is already in progress.");
        IReadOnlyList<Change> changes = Array.Empty<Change>();
        IReadOnlyList<ChangeResult> results = Array.Empty<ChangeResult>();
        IReadOnlyList<ImmediateToggleState> actual;
        Exception? failure = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Every ID/snapshot is captured before the first await. Never omit an
            // unchanged dependency: the worker requires exactly the complete group.
            var before = Read(recipe.Id);
            var readError = before.FirstOrDefault(state => !state.ReadSucceeded)?.Error;
            if (readError is not null) throw readError;
            changes = Array.AsReadOnly(before.Select(state => new Change(state.Id,
                recipe.Title + " · " + state.Id.ToString(CultureInfo.InvariantCulture), state.Snapshot!, target)).ToArray());
            FeatureRecipes.ValidateScope(recipe.Id, changes, device, observedIds, prerequisites);
            if (changes.Any(change => change.Before != change.After))
            {
                var returned = await execute(changes, recipe.Id, cancellationToken);
                if (returned is null || returned.Count == 0 || returned.Count > changes.Count)
                    throw new InvalidDataException("Invalid recipe execution response scope.");
                for (int i = 0; i < returned.Count; i++)
                    if (returned[i] is null || returned[i].Change != changes[i] || i < returned.Count - 1 && !returned[i].Applied)
                        throw new InvalidDataException("Recipe response changed scope or continued after a failure.");
                results = Array.AsReadOnly(returned.ToArray());
                if (results.Count != changes.Count || results.Any(result => !result.Applied))
                    failure = new InvalidOperationException(results.FirstOrDefault(result => !result.Applied)?.Error ?? "Recipe execution did not complete every feature ID.");
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            // Refresh every ID after success, denial, exception, cancellation, or a
            // partial write. Mixed/Default/read-error states never become an optimistic On.
            actual = Read(recipe.Id);
            Volatile.Write(ref busy, 0);
        }
        if (failure is null) failure = actual.FirstOrDefault(state => !state.ReadSucceeded)?.Error;
        if (failure is null && changes.Any(change => actual.Single(state => state.Id == change.Id).Snapshot != change.After))
            failure = new InvalidOperationException("Recipe read-back conflict: one or more overrides differ from the requested state.");
        return new(recipe, changes, results, actual, failure);
    }
}
