# Safety and recovery

The main process runs asInvoker. Browse/search/observe never elevate. Applying
spawns the same installed executable in a narrowly scoped worker mode via UAC.
The worker accepts at most 100 unique nonzero uint IDs and typed before/after
snapshots over a randomized current-user-only named pipe. It accepts no shell
commands, registry paths, filenames, download URLs, or arbitrary executables.
It independently validates the request, displays the exact IDs/states with a
No-default confirmation, preflights the batch, and exits after applying.
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
Do not delete your history before restoring a change.

If the process crashes, the UAC prompt is declined, the worker disconnects, or
saving results fails, the entry remains **pending / outcome uncertain**. Review
current state. Restore only proposes changes where current values exactly match
the recorded desired values; already-original values are skipped, and a different
value blocks the entire restore proposal. Pending is never called successful.
A changed state can have been produced by another program: a matching value is
not proof of ownership, so review pending restores carefully.

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
