# Application icon

An original ViVeUI mark created with ImageGen for this application: a white V
and cyan control node on a cobalt rounded square, without Microsoft logos.
The editable source asset is `src/ViVeUI.Windows/Assets/AppIcon.png`; the
application artwork is distributed under the repository license.

`tools/Build-Icon.swift` compiles the PNG into ICO frames at 16, 20, 24, 32, 40,
48, 64, 128 and 256 pixels using macOS AppKit. This is a format/size conversion,
not a dependency at runtime. The ICO is embedded as the PE application icon and
as a WPF resource for the window and dialogs; the same PNG appears in the sidebar.

Windows validation checks every embedded ICO frame, window icon presence and
shell icon extraction from the running executable. The clean-folder standalone
launch repeats icon extraction from the bundled EXE. ARM64 resource structure is
checked from its PE file; native ARM64 execution remains a manual check.
