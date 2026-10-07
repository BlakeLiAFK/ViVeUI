using Albacore.ViVe;
using Albacore.ViVe.NativeEnums;
using Albacore.ViVe.NativeStructs;
using Microsoft.Win32;
using ViVeUI.Core;

namespace ViVeUI.Windows;
public sealed class WindowsStore : IFeatureStore
{
    // Only the User (8) boot override is edited. Never touch policy, security,
    // variants, runtime experiments, LKG or other priorities.
    static string Key(uint id) => @"SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\" + ObfuscationHelpers.ObfuscateFeatureId(id);
    public Snapshot Read(uint id)
    {
        using var key = Registry.LocalMachine.OpenSubKey(Key(id));
        if (key is null) return Snapshot.Default;
        var names = key.GetValueNames();
        if (key.SubKeyCount != 0 || names.Length != 2 || !names.Contains("EnabledState") || !names.Contains("EnabledStateOptions") || key.GetValueKind("EnabledState") != RegistryValueKind.DWord || key.GetValueKind("EnabledStateOptions") != RegistryValueKind.DWord || key.GetValue("EnabledState") is not int state || state is < 0 or > 2 || key.GetValue("EnabledStateOptions") is not int options || options != 0)
            throw new InvalidOperationException("This override contains advanced or unrecognized properties. ViVeUI will not alter it.");
        return new(true, (OverrideState)state);
    }
    public void Write(uint id, Snapshot state)
    {
        // Revalidate the key immediately before any reset which deletes this specific key.
        Read(id);
        var update = new RTL_FEATURE_CONFIGURATION_UPDATE { FeatureId = id, Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User, EnabledState = (RTL_FEATURE_ENABLED_STATE)state.State, Operation = state.Exists ? RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState : RTL_FEATURE_CONFIGURATION_OPERATION.ResetState };
        var result = FeatureManager.SetFeatureConfigurations([update], RTL_FEATURE_CONFIGURATION_TYPE.Boot);
        if (result != 0) throw new IOException($"Windows rejected the change (0x{result:X8}).");
    }
    public static Dictionary<uint, string> Observe()
    {
        var values = FeatureManager.QueryAllFeatureConfigurations() ?? throw new IOException("Windows did not return feature configurations. Observations are unavailable.");
        return values.GroupBy(x => x.FeatureId).ToDictionary(x => x.Key, x => $"{x.First().EnabledState} · {x.First().Priority}");
    }
}
public sealed class DemoStore : IFeatureStore
{
    readonly Dictionary<uint, Snapshot> values = [];
    public Snapshot Read(uint id) => values.GetValueOrDefault(id, Snapshot.Default);
    public void Write(uint id, Snapshot state) => values[id] = state;
}
