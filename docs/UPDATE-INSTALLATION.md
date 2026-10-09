# Update installation and recovery

The v0.7 self-update implementation separates three actions: obtaining verified
bytes, replacing the application, and restarting it. Optional automatic downloads
authorize only the first action. They never silently install or launch an update.
This describes the implementation contract, not evidence of a completed Windows
installation test or a published v0.7 release.

## User flow

1. Check the configured GitHub repository for a newer release. Download the EXE
   matching the running architecture, manually or through the automatic-download
   preference. Download failure or verification failure prevents installation.
2. Explicitly request installation. This authorizes replacing the EXE at its current
   path, not launching an arbitrary executable from a caller-selected location.
   The helper rechecks release metadata independently and revalidates the package.
   A protected installation directory can require UAC; canceling it is not success.
3. After a verified replacement, choose whether to restart ViVeUI now. This is an
   application restart, not a Windows restart. Choosing later leaves the existing
   process running; the original path now contains the new version for a later launch.
4. On restart, the original UI launches the new EXE rather than asking the elevated
   helper to launch it. Startup failure triggers a rollback attempt. A new process
   surviving the initial check is not proof that every application feature works.

## Checks before replacement

The accepted source is the fixed `BlakeLiAFK/ViVeUI` GitHub repository over HTTPS.
The release tag must match the newer version. Asset name and URL must match the
exact tag and `ViVeUI-win-x64.exe` or `ViVeUI-win-arm64.exe` architecture. A nonempty
SHA-256 digest and a bounded positive size are required. The installer verifies:

- byte length and SHA-256 against trusted release metadata;
- a Windows executable PE image of the expected architecture, not a DLL;
- executable file version matching the release version;
- the installed EXE still matching the expected installed version and captured hash;
- the copied staging EXE before renaming and the final replacement afterward.

The helper resolves release metadata again itself; IPC cannot supply an invented
trusted digest. UI and helper authenticate each other using the existing worker
channel. Paths are derived locally, and the helper's returned target/hash/version
must match the expected scope. Symlink or junction update paths are rejected.

SHA-256 verification protects against mismatched or altered bytes relative to
GitHub's release metadata. It is not an Authenticode signature, independent publisher
attestation or protection against a compromised authorized release account. Published
bundles remain unsigned unless the release explicitly states otherwise.

## Backup, journal and interrupted operations

Installation stages a copy in the same directory. For a current path such as
`ViVeUI.exe`, the implementation uses:

| File | Purpose |
|---|---|
| `ViVeUI.exe.viveui-new.exe` | Verified staging copy, also used temporarily during rollback. |
| `ViVeUI.exe.viveui-backup.exe` | Previous executable retained for recovery. |
| `ViVeUI.exe.viveui-update.json` | Bounded journal containing old/new hashes, version and phase. |
| `ViVeUI.exe.viveui-update.lock` | Exclusive update coordination lock. |

A journal is written before copying so an interrupted copy has recovery context.
The original EXE is moved to the backup before the staged file takes its place.
Recovery checks hashes before trusting either file. Unexpected existing files,
malformed journals or unexpected hashes stop replacement instead of overwriting
unrecognized data. An interrupted operation can be reconciled or rolled back when
the installer next performs recovery.

The new process removes the backup and journal only after its running version and
target hash match the prepared update. A still-running old process or restricted
folder may prevent cleanup; files are retained for a later attempt. A restart
launch failure asks the helper to restore the previous EXE. If both the operation
and recovery fail, preserve the backup and journal and inspect the reported error;
there is no guarantee that every filesystem, antivirus lock or power-loss condition
can be recovered automatically.

A broken helper connection or timeout does not trigger an unsolicited launch.
There is no automatic uninstall of user data and no Windows-feature mutation in
the self-update path. Do not delete recovery files while an update is in progress.

Implementation: [installer and journal](../src/ViVeUI.Core/SelfUpdateInstaller.cs),
[Windows helper and restart session](../src/ViVeUI.Windows/SelfUpdateWorker.cs).
Exact test and release evidence belongs in [validation records](VALIDATION.md),
identified by its own version and commit; prior-version results do not validate
this new installation path.
