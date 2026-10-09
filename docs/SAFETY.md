# Safety and recovery

This document describes the local immediate-operation revision. Native Windows
validation of its new UI is pending; see [validation status](VALIDATION.md).

The main process runs asInvoker. Browse/search/observe never elevate. An immediate checkbox request
spawns the same installed executable in a narrowly scoped worker mode via UAC.
The worker accepts at most 100 unique nonzero uint IDs and typed before/after
snapshots over a randomized named pipe whose protected ACL grants only the current
user SID access. Both ends verify the peer PID and executable path, then authenticate
a random 256-bit challenge before accepting a request. This supports same-user
cross-integrity elevation without the incompatible CurrentUserOnly pipe option. It accepts no shell
commands, registry paths, filenames, download URLs, or arbitrary executables.
It independently validates the request, checks current versus expected state,
and exits after applying. The immediate flow has no additional app confirmation
or staging queue; Windows UAC still controls elevation.
A different administrator account during UAC cannot access the original user's
pipe: use an administrator account with same-user elevation. Cancellation is safe.

Each override is limited to HKLM's FeatureManagement Overrides priority 8 key
for the obfuscated ID. Only keys with exactly DWORD EnabledState (0–2) and DWORD
EnabledStateOptions=0, without children, are writable. Keys containing variants,
other values, malformed data, or unknown options are refused. No global reset.

The unelevated UI writes a durable intent journal before starting the worker,
then writes per-item verification results after the worker replies. Journals
live in `%LOCALAPPDATA%\ViVeUI\history`; demo data uses `ViVeUI-Demo` separately.
They include full IDs, previous/desired snapshots, timestamps, and Windows build.
History is view-only; it does not replay operations or restore earlier snapshots.
Keep records when investigating a change. Restore default is a separate action for
the selected feature and removes only that feature’s explicit user override.

If the process crashes, the UAC prompt is declined, the worker disconnects, or
saving results fails, the entry remains **pending / outcome uncertain**. Review
current state before making another immediate request. Pending is never called
successful. A changed state may have been produced by another program; a matching
value is not proof of ownership. History does not offer scoped undo, and removing
an override with Restore default does not recreate an earlier explicit override.

The registry API does not provide compare-and-swap for these boot keys. There is
a residual external-writer race between last comparison and write. Do not run
other feature configuration tools concurrently. Batches are not transactions:
stop-on-error protects later entries but does not roll back earlier successful
ones. Read-back mismatch is a failure; history retains the original snapshots.

After apply, restart manually. A user-priority boot override is not the effective
runtime state; higher priorities, dependencies, server rollout and build-specific
behavior can supersede it. Default means removing this user override, not disabling.
The worker never rewrites advanced keys or touches security/policy priorities.

If Windows becomes unusable, use your previously prepared Windows recovery
method. ViVeUI does not promise to repair an unbootable system and does not
silently change recovery configuration. Offline registry repair is out of scope.

No automated test writes real feature settings, including Windows CI smoke tests.

## Historical references and native instructions

All 60 source-linked feature archives are read-only. Known reference IDs, including
conditional dependencies, reject new Enabled/Disabled mutations at the UI request, application,
pre-elevation and worker validation boundaries. Default removal is allowed for recovery only;
removing an override is not proof that Windows will boot or behave correctly.
Unmapped raw IDs remain an explicitly advanced, unverified workflow.

Native cards only open fixed navigation destinations or source documentation.
External Windows settings are outside ViVeUI history and cannot be automatically
undone by it. Destructive consequences, previously exposed data and completed
commands are never represented as reversibly toggled preferences.
