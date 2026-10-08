# v0.5.0 curated catalog input contract

Domain files in `catalog-work/research/<domain>.json` are arrays of objects:

```json
{"id":"ClassicMenu","kind":"NativeShortcut","category":"ContextMenu","title":"Classic context menu","body":"Right-click a file and choose Show more options. On documented builds, Shift + right-click opens the full menu; this does not permanently replace it.","keywords":"right click classic context menu","sources":["https://support.microsoft.com/..."],"evidence":"Windows 11; Shift + right-click: Dev 22572 (2022-03-09). Current device support unverified.","featureIds":[],"destination":"explorer.exe","risk":"None","restart":"None","restore":"None","illustration":"Context"}
```

- All 14 fields required. Unique stable PascalCase id; never recycle an ID for another operation.
- kind: NativeSettings, NativeShortcut, Guide, Historical, FeatureFlag. Historical is read-only. FeatureFlag requires separately reviewed direct ID/build evidence; do not use it during research by default.
- category: ContextMenu, Explorer, Taskbar, Start, Windows, Input, Accessibility, Appearance, Notifications, Performance, SystemTools, Privacy.
- risk: None, UnsavedWork, Files, Privacy, Power, Accessibility, Experimental, Network, Security. Do not invent unsupported registry changes.
- restart: None, App, SignOut, Device, Varies.
- restore: None, PreviousSetting, CloseView, Backup, Manual.
- illustration: Context, Explorer, Taskbar, Layout, Sound, Settings, Widgets, Search (existing resources checked during integration).
- sources: primary documentation actually read; mapping references as extra URLs. Source must support the exact instructions. Evidence metadata must distinguish historic build from current availability.
- featureIds: integer array only if ID association has direct evidence; dictionary name alone is not behavioral proof. Historical entries never stage changes.
- destination: null, explorer.exe, or a documented exact ms-settings URI (integration allowlist required); no caller-controlled commands, registry writes, argument strings or arbitrary URLs here.
- body: about 25–55 words; one distinct useful task with concrete steps. No filler, split inverse toggles, duplicate shortcuts for same purpose, or generic Windows trivia.
- 16 language files later translate title, body, keywords and narrative evidence. Shared risk/restart/restore enums use localized common text.
- Each research file should have an adjacent `<domain>-sources.md` explaining sources checked and unsupported candidates rejected. Do not count rejected candidates.
