# Feature recipes

The v0.7 implementation adds a feature-focused home page with **7 recipes and 9
unique IDs**. The separate 213-entry editorial catalog remains a collection of
native settings, guides, shortcuts and read-only historical references. It is not
213 actionable feature switches, and the seven recipes do not add new entries to
that editorial count.

These are original-discovery recipes with corroborating Microsoft feature/build
announcements. Microsoft announcements describe features, not endorsement of
ViVe override commands. Sources are embedded in each recipe. Current-device visual
behavior has not been established merely by sourcing a command or reading an ID.

## Included scope

| Recipe | Complete ID group | Exact reported builds and channels | Important boundary |
|---|---|---|---|
| Voice typing profanity-filter choice | `33289874` | Dev `26200.5570`; Beta `26120.3941`, `22635.5305` | Exposes a filtering choice; enabling the flag does not itself turn the profanity filter off. |
| Grouped accessibility Quick Settings | `56887314` | Dev `26200.5570`; Beta `26120.3941` | Reorganizes controls; does not add hardware capabilities. |
| Voice Access custom vocabulary | `56305056` | Dev `26200.5562`; Beta `26120.3872` | Exposes a vocabulary builder; language setup and recognition remain separate. |
| Voice Access in Quick Settings | `56724497` | Dev `26200.5562`; Beta `26120.3872` | Adds an entry point; does not initialize Voice Access or install language components. |
| Image editing in Windows Share | `53433910`, `54475355` | Dev `26200.5516`; Beta `26120.3653` | One two-ID recipe for local images; the web-link preview ID is not included. |
| Expanded HDR settings | `49793372` | Dev `26200.5581`; Beta `26120.3950` | Requires suitable display capabilities; Dolby Vision needs a Dolby Vision display. |
| Explorer image AI actions | `54792954`, `55345819` | Dev `26200.5603`; Beta `26120.4151` | One two-ID recipe; it does not promise Microsoft 365 document actions or waive app/subscription requirements. |

The image-editing announcement lists `.jpg`, `.jpeg`, `.dib`, `.png`, `.tif`, `.tiff`
and `.bmp`. The Explorer AI rollout was later restarted in Dev `26200.5661` after
some Insiders lost the menu. This is a concrete reason not to interpret an observed
build as “this version and everything newer.”

## Applicability is separate from override state

The three home-page filter checkboxes select **Applicable**, **Not applicable** and
**Unconfirmed** recipes. Applicable and Unconfirmed are shown initially; Not
applicable is initially hidden. Filter counts reflect the search results before
visibility choices. Filters and search do not write Windows configuration.

The implemented evaluator uses these rules:

| Result | Evidence rule |
|---|---|
| Not applicable | Windows base build is below `22000`, a recipe ID is absent from current discovery, or a declared prerequisite is known false. |
| Unconfirmed | UBR/channel evidence or prerequisite evidence is missing, or the complete build/revision/channel does not match a recorded observation. |
| Applicable | All recipe IDs are observed, declared prerequisites are satisfied, and build, UBR and channel exactly match one recorded observation. |

UBR is the update revision after the build number: `5570` in `26200.5570`. A base
build match alone is insufficient. Channel matching is case-insensitive, but an
unrecognized branch label is not guessed to mean Dev or Beta. Unreadable device
metadata remains unknown. The current HDR prerequisite is not automatically proven
by simply finding the feature ID; absent display-capability evidence keeps it
Unconfirmed.

“Applicable” is an evidence classification, not verified visual compatibility.
The override display independently reports enabled, disabled, default, mixed or
read failure. A successful query or write/read-back cannot establish that the
feature appears, works correctly, or remains present after later Windows updates.

## Applying a complete recipe

An eligible control immediately requests one uniform enabled, disabled or absent
Default target for the **entire approved group**. An Unconfirmed recipe can be
attempted when discovery and state reads permit it; this remains an experiment.
Not applicable recipes are blocked. The worker rechecks its own device evidence
and accepts exactly the IDs assigned to the trusted recipe identifier. Extra,
missing or differently targeted IDs are rejected.

Before snapshots are captured for every ID, including members already at the
requested state. All members are reread after success, denial, cancellation or
failure. The operation is not an atomic Windows transaction: an earlier ID can
change before a later one fails. Read the per-ID results and mixed-state display;
there is no automatic promise to reverse already completed writes.

The raw/manual historical guard remains in place. A historical ID cannot bypass
that guard through arbitrary manual enable/disable calls merely because it belongs
to one of these recipes. Its dedicated recipe path has a separate, bounded scope.
History remains read-only. Windows default removes the group's explicit user
overrides; it is not restoration of an arbitrary prior snapshot. Disable is an
explicit off override and is not synonymous with default or visual rollback.

The original discovery commands principally establish enable recipes. They do not
independently prove disable behavior, a complete absence of additional dependencies,
or a no-restart guarantee. The app does not restart Windows for a recipe.

## Source map

- Voice typing: [original discovery](https://bsky.app/profile/phantomofearth.bsky.social/post/3lnnsubw5wq2c), [Microsoft Dev 26200.5570](https://blogs.windows.com/windows-insider/2025/04/25/announcing-windows-11-insider-preview-build-26200-5570-dev-channel/).
- Accessibility grouping: [original discovery](https://bsky.app/profile/phantomofearth.bsky.social/post/3lnnt6zetnd2s), [Microsoft Dev 26200.5570](https://blogs.windows.com/windows-insider/2025/04/25/announcing-windows-11-insider-preview-build-26200-5570-dev-channel/).
- Both Voice Access recipes: [original discovery assigning separate IDs](https://bsky.app/profile/phantomofearth.bsky.social/post/3lnebixohct2k), [Microsoft Dev 26200.5562](https://blogs.windows.com/windows-insider/2025/04/21/announcing-windows-11-insider-preview-build-26200-5562-dev-channel/).
- Share image editing: [original paired command](https://bsky.app/profile/phantomofearth.bsky.social/post/3llhrij7bn22s), [Microsoft Dev 26200.5516](https://blogs.windows.com/windows-insider/2025/03/28/announcing-windows-11-insider-preview-build-26200-5516-dev-channel/).
- HDR: [original discovery, May 6](https://bsky.app/profile/phantomofearth.bsky.social/post/3loidxv5kpg2s), [Microsoft Dev 26200.5581, May 5](https://blogs.windows.com/windows-insider/2025/05/05/announcing-windows-11-insider-preview-build-26200-5581-dev-channel/).
- Explorer AI: [original paired command](https://bsky.app/profile/phantomofearth.bsky.social/post/3lpkladfnbm2a), [Microsoft Dev 26200.5603](https://blogs.windows.com/windows-insider/2025/05/19/announcing-windows-11-insider-preview-build-26200-5603-dev-channel/), [later rollout restart](https://blogs.windows.com/windows-insider/2025/06/23/announcing-windows-11-insider-preview-build-26200-5661-dev-channel/).

Implementation: [trusted recipe policy](../src/ViVeUI.Core/FeatureRecipes.cs),
[grouped operation controller](../src/ViVeUI.Core/RecipeToggleController.cs),
[Windows device evidence](../src/ViVeUI.Windows/WindowsDevice.cs).
This document states implementation contracts; it does not claim completed native
feature-mutation tests or a published v0.7 release.
