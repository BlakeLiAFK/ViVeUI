# Design rationale and research

Reviewed October 7, 2026. The visual language is original: graphite navigation,
ivory surfaces, cobalt actions, quiet spacing, native Segoe UI typography and
thin-line conceptual illustrations. It synthesizes principles rather than copying
award-winning screens, imagery, or branding.

## Recent award references

- [2026 Apple Design Awards](https://www.apple.com/newsroom/2026/06/apple-reveals-winners-of-the-2026-apple-design-awards/): Moonlitt's approachable interaction informed progressive disclosure; Tide Guide's information presentation informed distinct catalog/observation/override layers; Guitar Wiz's inclusive approach informed text scaling and non-color state labels.
- [Apple Design Awards gallery](https://developer.apple.com/design/awards/).
- [Red Dot 2025 Chem Watch](https://www.red-dot.org/project/chemical-spills-the-quiet-catastrophe-84990): multilingual safety communication informed warning and workflow translations.
- [Red Dot 2025 Wohin·Du·Willst 4.0](https://www.red-dot.org/project/wohinduwillst-40-84943): unified accessible interaction informed a consistent navigation and review pattern.

## Decisions

Catalog selection opens a detail panel; it never changes system configuration.
The persistent review bar communicates the queue. Tri-state selection avoids
conflating default and disabled. Review shows actual IDs and before/after states.
Current observation, user override, historical illustration and unknown behavior
are distinct. History is snapshot-based and never advertises a global “undo all”.
Updates distinguish checking, available, download/verification, ready, and failed.
A download is a file awaiting manual installation. Automatic checks and downloads
are explicit persistent preferences, off initially.

Native buttons, lists, text boxes, checkboxes, focus outlines, automation labels,
Ctrl+F, virtualized catalog rows, scalable layout, and system high-contrast brushes
support basic accessibility. English, Simplified Chinese and Spanish are included;
identifiers, build strings and original OS diagnostics are intentionally unchanged.
No image contains translated text. WPF supports flow direction but a full RTL
translation is not shipped; RTL and Narrator behavior still require human validation.
At compact widths the catalog and detail become separate pages with explicit
Open details / Back controls; minimum width is 880 logical pixels.

Guidance:
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-checklist
- https://learn.microsoft.com/en-us/windows/apps/design/globalizing/adjust-layout-and-fonts--and-support-rtl

These references explain design choices, not an assertion that ViVeUI won an award
or passed independent accessibility certification.
