namespace ViVeUI.Core;

public sealed record ImmediateToggleState(uint Id, Snapshot? Snapshot, Exception? Error)
{
    public bool ReadSucceeded => Snapshot is not null && Error is null;
    // Default is an absent/neutral override, never an inferred disabled feature.
    public bool? State => ReadSucceeded && Snapshot!.Exists ? Snapshot.State switch
    {
        OverrideState.Enabled => true,
        OverrideState.Disabled => false,
        _ => null
    } : null;
}

public sealed record ImmediateToggleOutcome(Change? Change, IReadOnlyList<ChangeResult> Results,
    ImmediateToggleState Actual, Exception? Error)
{
    public bool Canceled => Error is OperationCanceledException;
    public bool Succeeded => Error is null && Actual.ReadSucceeded && Change is not null &&
        Actual.Snapshot == Change.After && (Change.Before == Change.After && Results.Count == 0 ||
        Results.Count == 1 && Results[0].Change == Change && Results[0].Applied);
}

/// <summary>
/// Executes one captured feature override at a time. The caller owns confirmation,
/// elevation, history persistence and UI selection; the backend must enforce the
/// supplied Before snapshot again immediately before writing (as ChangeEngine does).
/// </summary>
public sealed class ImmediateToggleController(IFeatureStore store,
    Func<IReadOnlyList<Change>, CancellationToken, Task<IReadOnlyList<ChangeResult>>> execute)
{
    readonly IFeatureStore store = store ?? throw new ArgumentNullException(nameof(store));
    readonly Func<IReadOnlyList<Change>, CancellationToken, Task<IReadOnlyList<ChangeResult>>> execute =
        execute ?? throw new ArgumentNullException(nameof(execute));
    int busy;
    public bool IsBusy => Volatile.Read(ref busy) != 0;

    public ImmediateToggleState Read(uint id)
    {
        try
        {
            if (id == 0) throw new InvalidDataException("Invalid feature ID.");
            var snapshot = store.Read(id);
            if (snapshot is null || !Enum.IsDefined(snapshot.State) || !snapshot.Exists && snapshot.State != OverrideState.Default)
                throw new InvalidDataException("Invalid feature read-back snapshot.");
            return new(id, snapshot, null);
        }
        catch (Exception error) { return new(id, null, error); }
    }

    public async Task<ImmediateToggleOutcome> ApplyAsync(Feature feature, Snapshot target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(target);
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            throw new InvalidOperationException("A feature override operation is already in progress.");

        // Immutable values and the preflight snapshot are captured before the first await.
        var id = feature.Id;
        Change? change = null;
        IReadOnlyList<ChangeResult> results = Array.Empty<ChangeResult>();
        Exception? failure = null;
        ImmediateToggleState actual;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = Read(id);
            if (!before.ReadSucceeded) throw before.Error!;
            change = new(id, feature.Name, before.Snapshot!, target);
            ChangeEngine.Validate([change]);
            CuratedCatalog.ValidateOverrideMutation(id, target);
            if (change.Before != change.After)
            {
                var returned = await execute(Array.AsReadOnly(new[] { change }), cancellationToken);
                if (returned is null || returned.Count != 1 || returned[0] is null || returned[0].Change != change)
                    throw new InvalidDataException("Backend returned a different feature operation scope.");
                results = Array.AsReadOnly(returned.ToArray());
                if (!results[0].Applied) failure = new IOException(results[0].Error ?? "Feature override was not applied.");
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            // Never optimistically display the requested value, even after cancellation
            // or a backend exception that may have occurred after a partial write.
            actual = Read(id);
            Volatile.Write(ref busy, 0);
        }
        if (failure is null && !actual.ReadSucceeded) failure = actual.Error;
        if (failure is null && change is not null && actual.Snapshot != change.After)
            failure = new IOException("Read-back conflict: the actual override differs from the requested state.");
        return new(change, results, actual, failure);
    }
}
