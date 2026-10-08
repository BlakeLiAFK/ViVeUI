# Catalog and image provenance

Source: https://github.com/thebookisclosed/ViVe/blob/3f8c6a3425983412da1e8b26cd757c3aa17b3f25/Extra/FeatureDictionary.pfs

Pinned commit: `3f8c6a3425983412da1e8b26cd757c3aa17b3f25` (March 10, 2025).
Git blob: `6d82dcea74a0be7c19be07051e7d1a4385b2a4c3`.
Size: 608,083 bytes. Parsed count: 17,000 unique nonzero uint IDs.
The last line has no newline, so `wc -l` alone reports 16,999.
The original file and all source notices are retained unchanged under GPL-3.0+.

This catalog is an upstream snapshot, not a universal Windows list. No other
unlicensed feature dumps are redistributed. Unknown custom uint IDs can be
inspected, with explicitly unknown catalog context. Missing runtime observations
cannot establish whether an ID is supported or disabled.

Names remain original identifiers in every language. Editorial text is separate
from them. Only five historical examples are categorized; no behavior or category
is guessed from opaque names. Exact OS applicability and dependencies are unknown.

| Historical example ID | Upstream name / category | Historical context |
|---|---|---|
| 37634385 | TIFE / Explorer | [Build 25136](https://blogs.windows.com/windows-insider/2022/06/09/announcing-windows-11-insider-preview-build-25136/) |
| 36354489 | SV2Navpane / Explorer | [Build 25136](https://blogs.windows.com/windows-insider/2022/06/09/announcing-windows-11-insider-preview-build-25136/) |
| 34300186 | WidgetsExpandedView / Widgets | [Build 25201](https://blogs.windows.com/windows-insider/2022/09/14/announcing-windows-11-insider-preview-build-25201/) |
| 39420424 | TaskManagerFilterEngine / System | [Build 22621/22623.891](https://blogs.windows.com/windows-insider/2022/11/10/announcing-windows-11-insider-preview-build-22621-891-and-22623-891/) |
| 40430431 | LiveKernelDump / System | [Build 25276](https://blogs.windows.com/windows-insider/2023/01/12/announcing-windows-11-insider-preview-build-25276/) |

The Microsoft posts describe historical behavior, not official feature-ID mappings.
Community mapping corroboration: https://github.com/PeterStrick/ViVeTool-GUI/issues/19
These entries are illustrative, not recommendations for a modern build.

Images are original vector schematics compiled into the WPF UI, under the same
GPL license. They label conceptual categories; no third-party Windows screenshots
or trademark assets are bundled. Unknown flags show “No verified visual preview”.
UI screenshot previews in docs are renders of ViVeUI's own demo mode, not Windows
Insider feature screenshots. Microsoft screenshot usage restrictions informed this
choice: https://www.microsoft.com/en-us/legal/intellectualproperty/copyright/permissions

Upstream coverage explanation:
https://github.com/thebookisclosed/ViVe/wiki/Which-features-can-ViVeTool-toggle%3F

## Sourced guides in 0.4.0

The default gallery has 20 entries: 15 guides and five historical experiments.
Guide records in `src/ViVeUI.Core/Guides.cs` are a separate type without a feature
ID or mutation command. Native buttons open fixed, allowlisted Windows pages or
Explorer; they never change a setting. Settings availability is documented
evidence, not a monotonically increasing minimum-build guarantee.

| Topic | Mechanism and evidence | Source |
|---|---|---|
| Classic context menu | Show more options; Shift + right-click documented in Dev 22572 | [Explorer](https://support.microsoft.com/en-gb/windows/experience/fileexplorer/file-explorer-in-windows), [22572](https://blogs.windows.com/windows-insider/2022/03/09/announcing-windows-11-insider-preview-build-22572/) |
| Customizable context menu | Experimental rollout only: 26340.9212, 28120.2738, 29648.1000; no force-enable ID | [Design announcement](https://blogs.windows.com/windows-insider/2026/08/17/improving-file-explorer-context-menu-faster-simpler-and-more-customizable/), [Build/channel announcement](https://blogs.windows.com/windows-insider/2026/08/17/announcing-new-builds-for-17-august-2026/) |
| End task from taskbar | Native setting; Advanced page in 25H2, previously For developers; unsaved work risk | [Advanced settings](https://learn.microsoft.com/en-us/windows/advanced-settings/) |
| Taskbar combining/labels; volume mixer | Native options and Win+Ctrl+V documented in September 2023 configuration update | [Microsoft update](https://support.microsoft.com/en-us/servicing/os/configuration-updates/2023/08/september-26-2023-windows-configuration-update) |
| Smaller taskbar buttons | Gradual 24H2 preview rollout, 26100.4484 / KB5060829 | [Release notes](https://support.microsoft.com/en-gb/servicing/os/windows-11/2025/06/june-26-2025-kb5060829-os-build-26100-4484-preview) |
| Clock seconds | Native Date & time option; additional power use | [Microsoft instructions](https://support.microsoft.com/en-us/windows/experience/personalization/set-time-date-and-time-zone-settings-in-windows) |
| Extensions; hidden files; This PC start page | Native Explorer controls; no registry writes | [Explorer instructions](https://support.microsoft.com/en-gb/windows/experience/fileexplorer/file-explorer-in-windows) |
| Snap layouts; Edge tabs in Alt+Tab | Native multitasking controls | [Snap](https://support.microsoft.com/en-us/windows/experience/snap-your-windows), [Multitasking](https://support.microsoft.com/en-us/windows/how-to-multitask-in-windows-b4fa0333-98f8-ef43-e25c-06d4fb1d6960) |
| Energy recommendations | Native, hardware-dependent options | [Microsoft guide](https://support.microsoft.com/en-us/windows/experience/power-battery/learn-more-about-energy-recommendations) |
| Focus | Native sessions; stop session and review prior notification preferences | [Microsoft guide](https://support.microsoft.com/en-us/windows/experience/focus-stay-on-task-without-distractions-in-windows) |
| Clipboard history | Native setting; history and cloud sync are separate privacy decisions | [Microsoft guide](https://support.microsoft.com/en-us/windows/apps/using-the-clipboard) |

Windows destination URIs use the [official URI table](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings). Guides display scope, risk, documented version/channel, source, restart guidance and restoration boundaries. Restore the old value on the same Windows page; ViVeUI history cannot reverse external settings.

No verified universal ViVe recipe was found for permanently restoring the classic
context menu. Dictionary entries such as `CuratedHMenuContextMenu,29704337` prove
only an identifier/name pairing. They do not prove compatibility or a classic-menu
restore recipe. The app intentionally does not implement the frequently circulated
CLSID registry workaround or treat unsupported old taskbar implementations as safe
toggles. The classic-menu card instead prominently offers the supported menu path.

The fifth historical entry is `LiveKernelDump,40430431`, paired with the
[25276 diagnostic announcement](https://blogs.windows.com/windows-insider/2023/01/12/announcing-windows-11-insider-preview-build-25276/). It is not presented as current-build support; dumps can expose sensitive memory. The original 17,000-ID pinned dictionary is unchanged.
