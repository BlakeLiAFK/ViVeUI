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

A two-column illustrated gallery presents 213 curated entries in twelve-card pages.
Category and mechanism filters carry result counts. Cards use a three-line preview;
full instructions and evidence remain in the detail pane. Historical ViVe archives
are read-only; native Windows guides use source links and documented destinations.
Eligible advanced raw-ID operations retain fixed segmented state controls and review staging. A separate virtualized all-ID tab
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
identity is made. Mutation fixtures now use synthetic Demo IDs; historical IDs are exercised for search and rejection only.

Appearance offers system, light and dark settings; system high contrast takes
precedence. The original navigation-pane drawing is distinct from the tabs drawing.
At narrow widths, explore uses separate catalog/detail views and the review summary
moves below the cards with its own scroll area. Primary footer actions remain fixed.

The 0.2.1 refinement removes 37 px of excess Explore header spacing and trims
gallery artwork height from 170 to 155 px. Current-state values remain fixed above
the state controls while optional detail content scrolls. Native viewport checks
protect the first visible row and fixed actions at 1440×900; the expanded catalog scrolls within each page.

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

## 0.5.0 catalog scale

The full catalog separates 124 native settings, 25 instructions, four shortcuts and
60 source-linked read-only feature archives. Mechanism counts do not imply writable
features. Zero entries claim independent current-device verification. The default
first row keeps classic context-menu help beside the distinct modern-menu archive;
subsequent cards emphasize documented feature behavior before general settings.

Compact cards show a bounded preview rather than every caveat at once; selecting a
card opens complete localized instructions, source links, evidence, risk, restart
and restoration boundaries. Historical raw-ID detail uses the same evidence and
blocks enable/disable writes across the elevation boundary. A proposed history
restore is validated before merging into existing work.
