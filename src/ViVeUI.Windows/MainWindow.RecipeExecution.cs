using System.ComponentModel;
using ViVeUI.Core;

namespace ViVeUI.Windows;

public partial class MainWindow
{
    async Task<IReadOnlyList<ChangeResult>> ExecuteRecipeAsync(IReadOnlyList<Change> changes, string recipeId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        FeatureRecipes.ValidateScope(recipeId, changes, CurrentDeviceBuild, observations.Keys.ToHashSet());
        activeReceipt = new Receipt(Guid.NewGuid(), DateTimeOffset.Now, BuildText, changes.ToList());
        SaveReceipt(activeReceipt);
        try
        {
            var response = demo ? new WorkerResponse(ChangeEngine.Apply(store, changes), null)
                : await Program.ElevateAsync(changes.ToList(), L.Language, recipeId);
            if (response.Error is not null) throw new InvalidOperationException(response.Error);
            return response.Results ?? throw new InvalidDataException("Missing worker result.");
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("UAC canceled.", error);
        }
    }
}
