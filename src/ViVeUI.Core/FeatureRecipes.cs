using System.Collections.Frozen;

namespace ViVeUI.Core;

public sealed record DeviceBuild(int Build, int? Ubr, string? Channel = null);
public enum RecipeApplicability { Applicable, NotApplicable, Unconfirmed }
public sealed record FeatureRecipe(string Id, string Title, IReadOnlyList<uint> FeatureIds,
    IReadOnlyList<DeviceBuild> ObservedBuilds, IReadOnlyList<string> Sources,
    IReadOnlyList<string> Prerequisites, string Caveats);

public static class FeatureRecipes
{
    public const string HdrDisplay = "HdrDisplay";
    // Original discovery posts establish numerical recipes; Microsoft corroborates
    // their purpose and observed build, not endorsement of these override commands.
    // These explicit recipes do not change the archival/raw-ID mutation policy.
    public static IReadOnlyList<FeatureRecipe> All { get; } = Array.AsReadOnly(new FeatureRecipe[]
    {
        Create("VoiceTypingFilter", "Voice typing profanity-filter choice",
            [33289874], [new(26200, 5570, "Dev"), new(26120, 3941, "Beta"), new(22635, 5305, "Beta")],
            ["https://bsky.app/profile/phantomofearth.bsky.social/post/3lnnsubw5wq2c", "https://blogs.windows.com/windows-insider/2025/04/25/announcing-windows-11-insider-preview-build-26200-5570-dev-channel/"], [],
            "Enabling the feature exposes a choice; it does not itself mean the profanity filter is turned off. No restart or dependency requirement is supplied by the original post. No compatible-build range or current-device result was established. Not specified by original discovery post; do not invent a guaranteed no-restart claim. Query/set success confirms override state only, not visible feature availability. Disabling was not independently verified by the enable-only source; Default removes the user override rather than proving visual rollback."),
        Create("QuickAccessibilityGroups", "Grouped accessibility Quick Settings",
            [56887314], [new(26200, 5570, "Dev"), new(26120, 3941, "Beta")],
            ["https://bsky.app/profile/phantomofearth.bsky.social/post/3lnnt6zetnd2s", "https://blogs.windows.com/windows-insider/2025/04/25/announcing-windows-11-insider-preview-build-26200-5570-dev-channel/"], [],
            "This is a navigation reorganization, not additional hardware capabilities. Original post names a single ID without extra dependencies or restart instructions. Current-device behavior is untested. Not specified by original discovery post; do not invent a guaranteed no-restart claim. Query/set success confirms override state only, not visible feature availability. Disabling was not independently verified by the enable-only source; Default removes the user override rather than proving visual rollback."),
        Create("VoiceVocabulary", "Voice Access custom vocabulary",
            [56305056], [new(26200, 5562, "Dev"), new(26120, 3872, "Beta")],
            ["https://bsky.app/profile/phantomofearth.bsky.social/post/3lnebixohct2k", "https://blogs.windows.com/windows-insider/2025/04/21/announcing-windows-11-insider-preview-build-26200-5562-dev-channel/"], [],
            "Original post assigns this ID specifically to vocabulary, separately from the Quick Settings toggle. Voice Access language setup and recognition remain distinct from exposing the builder. Current-device behavior and additional unlisted dependencies are unverified. Not specified by original discovery post; do not invent a guaranteed no-restart claim. Query/set success confirms override state only, not visible feature availability. Disabling was not independently verified by the enable-only source; Default removes the user override rather than proving visual rollback."),
        Create("VoiceQuickSettings", "Voice Access shortcut in Quick Settings",
            [56724497], [new(26200, 5562, "Dev"), new(26120, 3872, "Beta")],
            ["https://bsky.app/profile/phantomofearth.bsky.social/post/3lnebixohct2k", "https://blogs.windows.com/windows-insider/2025/04/21/announcing-windows-11-insider-preview-build-26200-5562-dev-channel/"], [],
            "This exposes a shortcut and does not itself initialize Voice Access or language components. Do not combine with the vocabulary ID as a required pair: the discoverer assigns different purposes. Original post specifies no restart; device behavior is untested. Not specified by original discovery post; do not invent a guaranteed no-restart claim. Query/set success confirms override state only, not visible feature availability. Disabling was not independently verified by the enable-only source; Default removes the user override rather than proving visual rollback."),
        Create("SharePhotoEditing", "Image editing inside Windows Share",
            [53433910, 54475355], [new(26200, 5516, "Dev"), new(26120, 3653, "Beta")],
            ["https://bsky.app/profile/phantomofearth.bsky.social/post/3llhrij7bn22s", "https://blogs.windows.com/windows-insider/2025/03/28/announcing-windows-11-insider-preview-build-26200-5516-dev-channel/"], [],
            "The original command enables both IDs together; preserve the pair as one recipe. Microsoft limits this to local images and lists .jpg, .jpeg, .dib, .png, .tif, .tiff, and .bmp. Separate linked post maps web-link previews to 53871483; do not mistake that ID for part of image editing. Original post specifies no restart; current-device result is untested. Not specified by original discovery post; do not invent a guaranteed no-restart claim. Query/set success confirms override state only, not visible feature availability. Disabling was not independently verified by the enable-only source; Default removes the user override rather than proving visual rollback."),
        Create("HdrSettingsHistory", "Expanded HDR display settings",
            [49793372], [new(26200, 5581, "Dev"), new(26120, 3950, "Beta")],
            ["https://bsky.app/profile/phantomofearth.bsky.social/post/3loidxv5kpg2s", "https://blogs.windows.com/windows-insider/2025/05/05/announcing-windows-11-insider-preview-build-26200-5581-dev-channel/"], [HdrDisplay],
            "HDR streaming requires a capable HDR display. Dolby Vision mode requires a Dolby Vision display. An override does not create missing display capabilities. The Microsoft announcement is dated 2025-05-05; the mapping post is 2025-05-06. Original post specifies no dependencies or restart; actual device support is untested. Not specified by original discovery post; do not invent a guaranteed no-restart claim. Query/set success confirms override state only, not visible feature availability. Disabling was not independently verified by the enable-only source; Default removes the user override rather than proving visual rollback."),
        Create("ExplorerAiActions", "File Explorer image AI actions",
            [54792954, 55345819], [new(26200, 5603, "Dev"), new(26120, 4151, "Beta")],
            ["https://bsky.app/profile/phantomofearth.bsky.social/post/3lpkladfnbm2a", "https://blogs.windows.com/windows-insider/2025/05/19/announcing-windows-11-insider-preview-build-26200-5603-dev-channel/", "https://blogs.windows.com/windows-insider/2025/06/23/announcing-windows-11-insider-preview-build-26200-5661-dev-channel/"], [],
            "Preserve both IDs as one reported recipe; independent per-ID effects were not established. Do not promise Microsoft 365 document actions: those have separate Copilot/subscription/Insider requirements. Microsoft later says the rollout restarted in 26200.5661 after the menu had disappeared for some Insiders. This is positive evidence against treating 26200.5603 and every higher revision as a guaranteed supported range. Original post specifies no restart; installed app capabilities and current-device result are unverified. Not specified by original discovery post; do not invent a guaranteed no-restart claim. Query/set success confirms override state only, not visible feature availability. Disabling was not independently verified by the enable-only source; Default removes the user override rather than proving visual rollback."),
    });
    static readonly FrozenDictionary<string, FeatureRecipe> ById = All.ToFrozenDictionary(recipe => recipe.Id, StringComparer.Ordinal);
    public static FeatureRecipe Get(string id) => id is not null && ById.TryGetValue(id, out var recipe)
        ? recipe : throw new InvalidDataException("Untrusted feature recipe ID.");

    static FeatureRecipe Create(string id, string title, uint[] ids, DeviceBuild[] builds, string[] sources, string[] prerequisites, string caveats) =>
        new(id, title, Array.AsReadOnly(ids), Array.AsReadOnly(builds), Array.AsReadOnly(sources), Array.AsReadOnly(prerequisites), caveats);

    public static RecipeApplicability Evaluate(FeatureRecipe recipe, DeviceBuild device,
        IReadOnlySet<uint> observedIds, IReadOnlyDictionary<string, bool?>? prerequisites = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(observedIds);
        recipe = Get(recipe.Id); // Caller-created metadata cannot expand the trusted policy.
        if (device.Build < 22000)
            return RecipeApplicability.NotApplicable;
        bool unknownPrerequisite = false;
        foreach (var prerequisite in recipe.Prerequisites)
        {
            bool? present = prerequisites is not null && prerequisites.TryGetValue(prerequisite, out var value) ? value : null;
            if (present == false) return RecipeApplicability.NotApplicable;
            if (present is null) unknownPrerequisite = true;
        }
        // QueryAllFeatureConfigurations enumerates configurations, not every feature
        // compiled into every Windows binary. Absence is inconclusive, just as presence
        // or set success cannot establish a visible behavior change. Upstream explains:
        // https://github.com/thebookisclosed/ViVe/wiki/Which-features-can-ViVeTool-toggle%3F
        if (recipe.FeatureIds.Any(id => !observedIds.Contains(id)) || unknownPrerequisite || device.Ubr is null || string.IsNullOrWhiteSpace(device.Channel))
            return RecipeApplicability.Unconfirmed;
        return recipe.ObservedBuilds.Any(build => build.Build == device.Build && build.Ubr == device.Ubr &&
            string.Equals(build.Channel, device.Channel, StringComparison.OrdinalIgnoreCase))
            ? RecipeApplicability.Applicable : RecipeApplicability.Unconfirmed;
    }

    // The worker must supply its own current discovery/build/prerequisite evidence.
    // UI-supplied observations are not an authorization boundary.
    public static FeatureRecipe ValidateScope(string recipeId, IReadOnlyList<Change> changes,
        DeviceBuild device, IReadOnlySet<uint> observedIds, IReadOnlyDictionary<string, bool?>? prerequisites = null)
    {
        var recipe = Get(recipeId);
        ChangeEngine.Validate(changes);
        if (changes.Count != recipe.FeatureIds.Count ||
            !recipe.FeatureIds.ToHashSet().SetEquals(changes.Select(change => change.Id)))
            throw new InvalidDataException("Recipe changes must contain exactly its full feature ID group.");
        var target = changes[0].After;
        if (target.Exists && target.State == OverrideState.Default || changes.Any(change => change.After != target))
            throw new InvalidDataException("Recipe changes require one uniform Enabled, Disabled, or absent Default target.");
        if (Evaluate(recipe, device, observedIds, prerequisites) == RecipeApplicability.NotApplicable)
            throw new InvalidDataException("Recipe is not applicable to the currently observed device capabilities.");
        // Unconfirmed observations remain explicit experimental attempts. Never claim
        // that a base-build match or successful write proves visible functionality.
        return recipe;
    }
}
