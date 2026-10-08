# Design rationale and research

Reviewed October 7, 2026. The visual language is original: light icon navigation with an active marker,
white and pale-blue surfaces, cobalt actions, quiet spacing, native Segoe UI typography and
thin-line conceptual illustrations. It synthesizes principles rather than copying
award-winning screens, imagery, or branding.

## Recent award references

- [2026 Apple Design Awards](https://www.apple.com/newsroom/2026/06/apple-reveals-winners-of-the-2026-apple-design-awards/): Moonlitt's approachable interaction informed progressive disclosure; Tide Guide's information presentation informed distinct catalog/observation/override layers; Guitar Wiz's inclusive approach informed text scaling and non-color state labels.
- [Apple Design Awards gallery](https://developer.apple.com/design/awards/).
- [Red Dot 2025 Chem Watch](https://www.red-dot.org/project/chemical-spills-the-quiet-catastrophe-84990): multilingual safety communication informed warning and workflow translations.
- [Red Dot 2025 Wohin·Du·Willst 4.0](https://www.red-dot.org/project/wohinduwillst-40-84943): unified accessible interaction informed a consistent navigation and review pattern.

## Decisions

A two-column illustrated gallery presents 20 curated entries. Historical ViVe
experiments use fixed segmented state controls and a staging action; native
Windows guides use source links and documented destinations. A separate virtualized all-ID tab
keeps all 17,000 entries available. Catalog selection never changes configuration.
Review pairs illustrated before/after cards with amber guidance, counts and three
steps, followed by explicit Back and Apply actions.
The persistent review bar communicates the queue. Tri-state selection avoids
conflating default and disabled. Review shows actual IDs and before/after states.
Current observation, user override, historical illustration and unknown behavior
are distinct. History is snapshot-based and never advertises a global “undo all”.
Updates distinguish checking, available, download/verification, ready, and failed.
A download is a file awaiting manual installation. Automatic checks and downloads
are explicit persistent preferences, off initially.

Native buttons, lists, text boxes, checkboxes, focus outlines, automation labels,
Ctrl+F, virtualized catalog rows, scalable layout, and system high-contrast brushes
support basic accessibility. Sixteen UI languages and Arabic RTL are included (see [coverage](LANGUAGES.md));
identifiers, build strings and original OS diagnostics are intentionally unchanged.
Original schematic images contain no translated text. Arabic resources and
mirrored layout are shipped and exercised by native fixtures; linguistic quality,
complex shaping and Narrator behavior still require human validation.
At compact widths the catalog and detail become separate pages with explicit
Open details / Back controls; minimum width is 900 logical pixels.

Guidance:
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-checklist
- https://learn.microsoft.com/en-us/windows/apps/design/globalizing/adjust-layout-and-fonts--and-support-rtl

These references explain design choices, not an assertion that ViVeUI won an award
or passed independent accessibility certification.

## Reference comparison

The supplied Library images could not be materialized in this build environment:
authorized downloads returned HTTP 403, including one retry, and image reads
returned no pixels. The originating session inspected those pixels and supplied
concrete comparison findings. Those findings informed the wider 438 px explore
panel, 370 px summary, larger artwork and text, selected-card outline, historical
badges, labeled state pills, sidebar language/appearance controls and review layout.
Actual Windows renders were inspected after these changes; no claim of exact pixel
identity is made. Chinese fixtures use IDs 37634385 and 39420424.

Appearance offers system, light and dark settings; system high contrast takes
precedence. The original navigation-pane drawing is distinct from the tabs drawing.
At narrow widths, explore uses separate catalog/detail views and the review summary
moves below the cards with its own scroll area. Primary footer actions remain fixed.

The 0.2.1 refinement removes 37 px of excess Explore header spacing and trims
gallery artwork height from 170 to 155 px. Current-state values remain fixed above
the state controls while optional detail content scrolls. Native viewport checks
protect all four cards and their actions at 1440×900 without reducing text size.

## 0.4.0 usability revision

The Discover catalog distinguishes shortcuts, Windows settings, Insider guides
and historical ViVe experiments. Guide actions open documented destinations;
they do not enter the privileged mutation queue. Classic context-menu help uses
Show more options and documented Shift + right-click behavior, not an invented
universal feature ID. Localized friendly text is searchable.

Dropdowns share the app palette, rounded surfaces, explicit selection marks,
hover/focus/disabled states and scroll controls. They retain WPF keyboard and
UI Automation behavior; Arabic popup direction is explicitly bound across its
separate native window. The original V icon replaces the plain letter mark.

Failed and unattempted changes stay in review with individual outcomes. History
restoration merges only after checking conflicts, preserving unrelated staged
work. Closing a nonempty queue defaults to Cancel. View scale persists with
bounded validation. Independent update cancellation keeps review editing usable
while a trusted package downloads; installation is never silent.
