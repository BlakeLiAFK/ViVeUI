# Current UI previews: pending native Windows rendering

The local revision replaces staging, Review and repeated app confirmations with
immediate checkbox operations. No screenshots of that revision have been generated
on Windows yet. The authoring host is macOS; compilation or a conceptual drawing
cannot establish how the native WPF controls render or behave.

[The original operation-flow schematic](immediate-toggle-flow.svg) illustrates the
intended interaction. It is labeled conceptual and is not an actual UI screenshot.
History is view-only; Restore default is a separate per-feature action.

There are deliberately no old screenshots presented as the current UI here or in
the project READMEs. [The v0.5.0 archive](archive/v0.5.0/README.md) retains the older
queued-workflow images, reports, implementation commit and Windows run provenance.
Those files are historical evidence only, including their Review/confirmation images.

## Regenerate actual local Windows previews

After execution on a Windows machine is authorized, run from the repository:

```powershell
pwsh tools/New-WindowsPreviews.ps1 -Run
```

The script builds locally, runs isolated fake-backend WPF fixtures and collects
new renders under a fresh `.artifacts/windows-previews-<id>/` directory. It never
runs the real mutation mode, an elevated IPC fixture, GitHub Actions, publication,
commit or push. Running the script without `-Run` only prints instructions.
A .NET SDK and restored build dependencies must already be available. If a restore
is needed, perform that separately when network use is authorized.

`preview-manifest.json` records the base Git commit, dirty-working-tree status,
source content digest, arguments, fixture reports and resulting image count.
A dirty tree is explicitly recorded, never passed off as a committed build.
Fixtures and the script must first be compatible with the new immediate workflow;
a failing fixture means previews are incomplete, not validated.

Before placing new images in current-facing documentation, inspect them for:

- One unified list, with Known content (153) and Historical references (60) checked
  initially, and Show unknown (16,944) unchecked; no filter dropdowns.
- At most twelve cards per page even with unknown entries; filters above, pager below.
- Separate Enter ID inspection: paste one decimal nonzero uint32 ID and press Enter,
  including unlisted IDs, without executing text or changing features.
- Exact numeric hidden matches prompt for a filter without automatically revealing content.
- Visibility filters are separate from the immediate Enable checkbox.
- Immediate enable/disable operation and distinct Windows-default state.
- Read-only historical cards and navigation-only native guides.
- Success, UAC cancellation and failure state feedback, without a staged queue.
- Compact layout, translated text, Arabic RTL and keyboard focus.

These are actual WPF control-tree renders with fake feature storage, not physical
monitor screenshots, Windows feature screenshots, or evidence of real system writes.
Do not copy archive files into the new output or label them as current screenshots.
