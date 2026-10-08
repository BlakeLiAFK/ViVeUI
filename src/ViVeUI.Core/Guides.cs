namespace ViVeUI.Core;
public enum GuideKind { NativeShortcut, NativeSettings, InsiderGuide }
public enum NativeDestination { None, Explorer, Taskbar, DateTime, Developers, Volume, Multitasking, Energy, Focus, Clipboard }
public sealed record WindowsGuide(string Key, GuideKind Kind, string Category, string Illustration, string Evidence, string Source, NativeDestination Destination = NativeDestination.None, string RiskKey = "GuideRiskSettings")
{
    public string TitleKey => "Guide" + Key + "Title";
    public string BodyKey => "Guide" + Key + "Body";
    public string RestoreKey => Kind == GuideKind.NativeShortcut ? "GuideRestoreNone" : "GuideRestoreSettings";
}
public static class GuideCatalog
{
    const string Support = "https://support.microsoft.com/en-us/windows/";
    const string Explorer = "https://support.microsoft.com/en-gb/windows/experience/fileexplorer/file-explorer-in-windows";
    const string September = "https://support.microsoft.com/en-us/servicing/os/configuration-updates/2023/08/september-26-2023-windows-configuration-update";
    public static IReadOnlyList<WindowsGuide> All { get; } = Array.AsReadOnly(new WindowsGuide[]
    {
        new("ClassicMenu", GuideKind.NativeShortcut, "Explorer", "Context", "Windows 11: Show more options; Shift + right-click: 22572 Dev, 2022-03-09", Explorer, NativeDestination.Explorer, "GuideRiskClassic"),
        new("ContextMenu", GuideKind.InsiderGuide, "Explorer", "Context", "Experimental: 26340.9212 / 28120.2738 / 29648.1000 · 2026-08-17", "https://blogs.windows.com/windows-insider/2026/08/17/improving-file-explorer-context-menu-faster-simpler-and-more-customizable/", RiskKey:"GuideRiskInsider"),
        new("EndTask", GuideKind.NativeSettings, "System", "Context", "Windows 11; System > Advanced: 25H2; earlier: For developers", "https://learn.microsoft.com/en-us/windows/advanced-settings/", NativeDestination.Developers, "GuideRiskEndTask"),
        new("TaskbarLabels", GuideKind.NativeSettings, "System", "Taskbar", "Windows 11 · 2023-09-26 configuration update", September, NativeDestination.Taskbar),
        new("TaskbarSmall", GuideKind.NativeSettings, "System", "Taskbar", "Windows 11 24H2 · 26100.4484 · KB5060829 Preview · gradual rollout", "https://support.microsoft.com/en-gb/servicing/os/windows-11/2025/06/june-26-2025-kb5060829-os-build-26100-4484-preview", NativeDestination.Taskbar),
        new("ClockSeconds", GuideKind.NativeSettings, "System", "Taskbar", "Windows 11 · Microsoft Support documentation checked 2026-10-08", Support+"experience/personalization/set-time-date-and-time-zone-settings-in-windows", NativeDestination.DateTime, "GuideRiskPower"),
        new("Volume", GuideKind.NativeSettings, "System", "Sound", "Windows 11 · 2023-09-26 configuration update", September, NativeDestination.Volume),
        new("Extensions", GuideKind.NativeSettings, "Explorer", "Explorer", "Windows 11 · Microsoft File Explorer documentation", Explorer, NativeDestination.Explorer, "GuideRiskFiles"),
        new("Hidden", GuideKind.NativeSettings, "Explorer", "Explorer", "Windows 11 · Microsoft File Explorer documentation", Explorer, NativeDestination.Explorer, "GuideRiskFiles"),
        new("ExplorerHome", GuideKind.NativeSettings, "Explorer", "Explorer", "Windows 11; Home naming: 22H2 · Microsoft documentation", Explorer, NativeDestination.Explorer),
        new("Snap", GuideKind.NativeSettings, "System", "Layout", "Windows 11 · Microsoft Snap documentation", Support+"experience/snap-your-windows", NativeDestination.Multitasking),
        new("AltTab", GuideKind.NativeSettings, "System", "Layout", "Windows 11 · Microsoft multitasking documentation", Support+"how-to-multitask-in-windows-b4fa0333-98f8-ef43-e25c-06d4fb1d6960", NativeDestination.Multitasking),
        new("Energy", GuideKind.NativeSettings, "System", "Settings", "Windows 11 22H2; settings URI documented for build 22624; hardware-dependent", Support+"experience/power-battery/learn-more-about-energy-recommendations", NativeDestination.Energy, "GuideRiskPower"),
        new("Focus", GuideKind.NativeSettings, "System", "Settings", "Windows 11 · Microsoft Focus documentation", Support+"experience/focus-stay-on-task-without-distractions-in-windows", NativeDestination.Focus),
        new("Clipboard", GuideKind.NativeSettings, "System", "Settings", "Windows 11 / Windows 10 · Microsoft clipboard documentation", Support+"apps/using-the-clipboard", NativeDestination.Clipboard, "GuideRiskClipboard")
    });
    public static IReadOnlyList<string> AdditionalSources { get; } = new[]
    {
        "https://blogs.windows.com/windows-insider/2022/03/09/announcing-windows-11-insider-preview-build-22572/",
        "https://blogs.windows.com/windows-insider/2026/08/17/announcing-new-builds-for-17-august-2026/",
        "https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings"
    };
    public static bool Matches(WindowsGuide guide, string query, string language) => string.IsNullOrWhiteSpace(query) ||
        new[] { Localization.Get(language,guide.TitleKey), Localization.Get(language,guide.BodyKey), guide.Key, guide.Evidence,
            guide.Key is "ClassicMenu" or "ContextMenu" or "EndTask" ? "right click context menu 右键 右鍵 classic 经典 傳統" : "" }.Any(s=>s.Contains(query.Trim(),StringComparison.OrdinalIgnoreCase));
    // Fixed destinations only: no caller-controlled commands, arguments, paths or registry targets.
    public static string? SettingsUri(NativeDestination destination) => destination switch
    {
        NativeDestination.Taskbar => "ms-settings:taskbar", NativeDestination.DateTime => "ms-settings:dateandtime", NativeDestination.Developers => "ms-settings:developers",
        NativeDestination.Volume => "ms-settings:apps-volume", NativeDestination.Multitasking => "ms-settings:multitasking",
        NativeDestination.Energy => "ms-settings:energyrecommendations", NativeDestination.Focus => "ms-settings:",
        NativeDestination.Clipboard => "ms-settings:clipboard", _ => null
    };
}
