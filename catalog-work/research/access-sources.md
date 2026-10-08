# Accessibility and input catalog normalization

Checked 2026-10-08. 48 distinct native settings or shortcut guides; no feature IDs and no system changes. All destinations only open a settings page. Minimum builds are explicitly unspecified unless the source supplies a release. Device support is never implied.

## Source review

- [see](https://support.microsoft.com/en-us/accessibility/windows/make-windows-easier-to-see)
- [input](https://support.microsoft.com/en-us/accessibility/windows/make-your-mouse-keyboard-and-other-input-devices-easier-to-use)
- [mouse](https://support.microsoft.com/en-us/windows/hardware/input-devices/change-mouse-settings)
- [mag](https://support.microsoft.com/en-us/accessibility/windows/magnifier/use-magnifier-to-make-things-on-the-screen-easier-to-see)
- [narrator](https://support.microsoft.com/en-us/accessibility/windows/narrator/chapter-7-customizing-narrator)
- [hear](https://support.microsoft.com/en-gb/accessibility/windows/make-windows-easier-to-hear)
- [caption](https://support.microsoft.com/en-us/accessibility/windows/use-live-captions-to-better-understand-audio)
- [voice](https://support.microsoft.com/en-us/accessibility/windows/voice-access/set-up-voice-access)
- [gesture](https://support.microsoft.com/en-us/windows/hardware/input-devices/touch-gestures-for-windows)
- [pen](https://support.microsoft.com/en-us/windows/hardware/input-devices/use-a-pen-with-windows)
- [display](https://support.microsoft.com/en-us/windows/hardware/display-graphics/change-display-brightness-and-color-in-windows)
- [rate](https://support.microsoft.com/en-us/windows/hardware/display-graphics/change-the-refresh-rate-on-your-monitor-in-windows)
- [hdr](https://support.microsoft.com/en-us/windows/hardware/display-graphics/hdr-settings-in-windows)
- [lang](https://support.microsoft.com/en-us/windows/hardware/input-devices/manage-the-language-and-keyboard-input-layout-settings-in-windows)
- [dict](https://support.microsoft.com/en-us/accessibility/windows/use-voice-typing-to-talk-instead-of-type-on-your-pc)
- [predict](https://support.microsoft.com/en-us/accessibility/windows/enable-text-suggestions-in-windows)
- [clip](https://support.microsoft.com/en-us/windows/apps/using-the-clipboard)
- [focus](https://support.microsoft.com/en-us/windows/experience/focus-stay-on-task-without-distractions-in-windows)
- [notify](https://support.microsoft.com/en-us/windows/experience/notifications-and-do-not-disturb-in-windows)
- [lock](https://support.microsoft.com/en-us/windows/experience/personalization/customize-the-lock-screen-in-windows)
- [Fixed settings URI reference](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)

All above pages were opened. Missing portions of the parent message were not treated as available records. Live captions (22H2+, with translation limited to Copilot+ / 24H2+), microphone captioning, caption style, voice access setup, ClickLock, pointer locator and inactive-window scrolling were reconstructed from the relevant original Microsoft pages. Live captions speech is local; voice typing uses online recognition; these are not interchangeable.

## Consolidation and exclusions

Magnifier zoom/view/inversion/follow is one entry. Narrator voice, detail and output are one setup entry; startup and ducking remain distinct outcomes. Cursor indicator/thickness, HDR enabling/SDR balance, night-light strength/schedule, voice typing/punctuation, and physical/multilingual suggestions were merged. Generic brightness, resolution, orientation, refresh-rate selection and scale basics were excluded. Shell-owned colors, accent and desktop background duplicates were excluded. Eye control and spatial sound were omitted rather than recovered from incomplete text without a scoped source review. Touchpad gesture directions are one customization entry, not one entry per finger/direction. Clipboard and Focus retain their existing stable IDs.

The Chinese companion fully adapts title, instructions, search keywords and evidence; remaining locales are the integration owner’s task. No native device testing was performed.
