# ViVeUI 0.6.0 native Windows previews

These 180 PNGs were rendered by actual WPF controls in
[Windows run 37889154249](https://github.com/BlakeLiAFK/ViVeUI/actions/runs/37889154249)
from source `850798ab335504a28ab65999b7342dc20d47e950`.
[PROVENANCE.json](PROVENANCE.json) records per-file SHA-256 digests.

The fixtures use isolated simulated feature storage. Images are native control-tree
renders, not physical desktop screenshots or images of experimental Windows features.
No real feature settings were changed. The source implementation and fixture files
are unchanged in the subsequent documentation/evidence commit.

- [Default paginated gallery](catalog/full-first-page.png)
- [Chinese direct checkbox](explore-zh.png)
- [Explicit enable](ux/checkbox-enabled.png), [disable](ux/checkbox-disabled.png), [Windows default](ux/checkbox-default.png), [failed operation](ux/checkbox-failed.png)
- [Hidden ID match](catalog/hidden-numeric-id.png), [explicit unknown visibility](catalog/unknown-numeric-id.png)
- [Unlisted manual ID](ux/manual-unlisted-id.png)
- [Arabic RTL](localization/ar/explore.png)
- [Read-only history](history.png)

Each of the sixteen language folders contains eight native captures. Catalog fixtures
exercise all curated pages, eight filter combinations and translated source details.
The reports alongside these images record their assertions and remaining manual-review
limits. [Full validation scope](../VALIDATION.md).

The [original flow schematic](immediate-toggle-flow.svg) is a labeled conceptual
diagram. Feature artwork is original schematic artwork, not third-party Windows screenshots.
The [v0.5.0 archive](archive/v0.5.0/README.md) preserves the old queued workflow separately.

To reproduce local renders on Windows, run `pwsh tools/New-WindowsPreviews.ps1 -Run`.
It records source hashes and dirty-tree status, performs no real feature writes and
neither commits nor publishes files. Physical DPI, Narrator, high-contrast interaction,
native-speaker linguistic review, secure-desktop UAC and ARM64 execution remain outside
these screenshots' evidence.
