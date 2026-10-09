using Microsoft.Win32;
using ViVeUI.Core;

namespace ViVeUI.Windows;

internal static class WindowsDevice
{
    internal static DeviceBuild Read()
    {
        var build = Environment.OSVersion.Version.Build;
        int? revision = null;
        string? channel = null;
        try
        {
            using var version = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (int.TryParse(version?.GetValue("CurrentBuildNumber") as string, out var registeredBuild)) build = registeredBuild;
            if (version?.GetValue("UBR") is int ubr && ubr >= 0) revision = ubr;
            using var selection = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\WindowsSelfHost\Applicability");
            channel = selection?.GetValue("BranchName") as string;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Missing servicing/channel evidence stays unknown; never infer it from a nearby build.
        }
        return new DeviceBuild(build, revision, channel);
    }
}
