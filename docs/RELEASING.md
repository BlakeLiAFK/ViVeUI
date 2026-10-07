# Release process and update trust

The Windows workflow builds and tests the pushed commit and emits two portable,
self-contained .NET 8 single-file executables, SHA256SUMS.txt, complete corresponding source,
and actual WPF demo screenshots. Publishing a release is a separate deliberate
maintainer action. CI does not silently publish or install.

For a stable release, use a semantic tag (`v0.2.1`) and GitHub release assets named
exactly `ViVeUI-win-x64.exe` and `ViVeUI-win-arm64.exe`, together with source and
checksums from the same successful commit. The GitHub release API must provide
`digest: sha256:<64 hex digits>` and a positive size for each binary EXE. Missing
digest, preview/draft release, wrong architecture or unexpected source is refused.

The updater reads only `api.github.com/repos/BlakeLiAFK/ViVeUI/releases/latest`.
It accepts release pages and initial download paths only for this repository.
HTTPS, standard ports, no embedded credentials. Download redirects are manually
validated against GitHub and GitHub's release-asset hosts, with a five-hop cap.
Streaming size is bounded to the declared size (maximum 250 MB). The digest is
computed incrementally before an atomic rename from a random partial file.
A failure deletes the partial and preserves a previously verified package.

The trusted digest comes from GitHub over TLS. This protects against truncated or
altered bytes, not a compromised repository/maintainer/GitHub account. Packages are
not Authenticode signed in this initial build. Do not describe digest checking as
publisher code signing. SmartScreen may warn for an unsigned application.

The app never extracts, replaces its own executable, launches the package, or
installs silently. Users open the download folder and run the chosen EXE themselves.
Each architecture is one self-contained EXE; .NET extracts bundled native files
and component notices into its per-user cache at launch. An administrator worker never downloads.

.NET 8 reaches end of support in November 2026; plan migration to the next LTS
before then. Packaging includes the runtime so rebuild releases when Microsoft
ships runtime security updates. Do not treat a past CI success as ongoing maintenance.
