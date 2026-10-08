# Feature archive evidence — 2026-10-08

Accepted: **62 distinct read-only feature archives**, comprising 42 original-discoverer recipes and 20 historical maintainer mappings. They reference **89 distinct IDs including prerequisites and related IDs**. That ID count is not a count of independent features. Every accepted ID occurs in a directly retrieved source associated with its entry. All 62 entries are `Historical`, with null destinations: no source-verified observation is represented as device-tested compatibility or an executable recommendation.

## Sources checked

Original discoveries were retrieved from PhantomOfEarth's public Bluesky account using `app.bsky.feed.getPosts`. The account DID was `did:plc:lld3hsasiketetu767uagr6m`. Thirty-nine command posts and their linked parent/quoted contexts were read; 56 posts were inspected in total. `flags-evidence.json` retains direct public post URLs, publication dates, AT URIs and content identifiers for each accepted recipe. It records reported commands as evidence, never as instructions for automatic execution. Media assets were neither downloaded nor licensed for reuse; use the application's original labeled schematics.

The historical mapping was read directly from [PeterStrick's issue 19](https://github.com/PeterStrick/ViVeTool-GUI/issues/19), authored by the upstream GUI maintainer and last updated 2025-05-30T19:21:01Z at retrieval. Its own introduction says it collects IDs from media and news sources. It is **secondary maintainer mapping**, not original experimental verification. Complete dependency recipes and current support remain unknown. Build labels are historical observations, never minimum-version checks.

[ExplorerPatcher discussion 2197](https://github.com/valinet/ExplorerPatcher/discussions/2197) was read directly. It reports Explorer crashing on 22621.2361 after disabling 36354489, 37634385 and 39145991, and reports removal of TIFE 37634385 in Dev 23575. Those warnings appear in the corresponding user-visible archive bodies. They are reported evidence, not reproduced crash tests.

## Counting and uncertainty decisions

- Camera media-type observations (49575624/50292326 on Dev 26120.2702 and 52142480 on Canary 27744) were merged into one archive, with build-specific alternatives. Do not run all three as one combination.
- Energy recommendation controls and their dark treatment were merged into one archive (39427030 and 41249924). The exact dependency relationship is unspecified.
- Six-ID Start redesign and six-ID Start customization each count as one archive. Taskbar overflow's two parts count as one archive.
- Spotlight 52145430 variants 1 and 2 are mutually exclusive alternatives within one archive, never sequential commands or separate feature counts.
- Required, optional and conditional dependencies are distinguished in bodies and evidence. Broad prerequisites never become independent entries.
- Three Settings dialog mappings are research-only. The mapping separately lists master 36390579 without proving the dependency or order; all remain read-only.
- Dark Run and dark Folder Options retained required 58383338 but dropped an additional 48433719 assertion not present in the directly retrieved post. The Share-with entry likewise excludes an unsubstantiated 48433719 prerequisite.
- The two battery-icon IDs from different dates were not presented as universally interchangeable. Only the directly retrieved 48822452 observation is retained in that entry.
- Four modern candidates could not be faithfully reconstructed from the truncated delegated messages without inventing missing source or recipe fields; they were omitted. This includes the incomplete Snap-label candidate and early All-apps grid candidate. The partial research payload is not a source of invented URLs.
- Current-device support remains unverified for all archives. An ID in the pinned dictionary, a successful override, and a visible change are separate facts.
- `disable` forces off; `reset` removes an override. Neither automatically restores a user's prior captured state. No full-reset recipe is offered.

## Validation performed

All 62 stable keys are unique, all 89 referenced IDs were found in their directly fetched source text, all destinations are null, and all entries have matching complete Simplified Chinese title/body/keywords/evidence. The five previous archive IDs remain represented: 37634385, 36354489, 34300186, 39420424 and 40430431. No Windows settings were modified and no feature recipe was executed.
