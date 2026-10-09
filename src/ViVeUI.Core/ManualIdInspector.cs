using System.Globalization;

namespace ViVeUI.Core;

public sealed class ManualIdQueryResult
{
    public uint Id { get; }
    public Snapshot? Snapshot { get; }
    public Exception? Error { get; }
    internal object Generation { get; }
    internal ManualIdQueryResult(uint id, Snapshot? snapshot, Exception? error, object generation)
        => (Id, Snapshot, Error, Generation) = (id, snapshot, error, generation);
}

/// <summary>Coordinates read-only inspection; consumers must check IsCurrent again before presenting a result.</summary>
public sealed class ManualIdInspector(Func<uint, CancellationToken, Task<Snapshot>> read) : IDisposable
{
    readonly Func<uint, CancellationToken, Task<Snapshot>> read = read ?? throw new ArgumentNullException(nameof(read));
    readonly object gate = new();
    object generation = new();
    CancellationTokenSource? pending;
    bool disposed;

    public bool IsCurrent(ManualIdQueryResult? result)
    {
        lock (gate) return !disposed && result is not null && ReferenceEquals(generation, result.Generation);
    }

    public async Task<ManualIdQueryResult?> QueryAsync(string text)
    {
        var current = new object();
        CancellationTokenSource cancellation;
        CancellationTokenSource? previous;
        lock (gate)
        {
            if (disposed) return null;
            generation = current; // Invalid input must invalidate an earlier valid read too.
            previous = pending;
            pending = cancellation = new();
        }
        Cancel(previous);
        try
        {
            var value = text?.Trim();
            if (string.IsNullOrEmpty(value) || value.Any(c => c is < '0' or > '9') ||
                !uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0)
                throw new FormatException("Enter a nonzero decimal feature ID between 1 and 4294967295.");

            Snapshot? snapshot = null;
            Exception? error = null;
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                snapshot = await read(id, cancellation.Token).ConfigureAwait(false);
                if (snapshot is null || !Enum.IsDefined(snapshot.State) || !snapshot.Exists && snapshot.State != OverrideState.Default)
                    throw new InvalidDataException("Invalid feature read-back snapshot.");
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception failure) { snapshot = null; error = failure; }

            lock (gate)
                return disposed || !ReferenceEquals(generation, current) || cancellation.IsCancellationRequested
                    ? null : new(id, snapshot, error, current);
        }
        finally
        {
            lock (gate) { if (ReferenceEquals(pending, cancellation)) pending = null; }
            cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? previous;
        lock (gate)
        {
            if (disposed) return;
            disposed = true; generation = new(); previous = pending; pending = null;
        }
        Cancel(previous);
    }

    static void Cancel(CancellationTokenSource? cancellation)
    {
        // A finished read may dispose its source concurrently. Cancellation is advisory;
        // generation identity remains authoritative even if a reader ignores the token.
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { } // A reader's faulty cancellation callback cannot revive stale data.
    }
}
