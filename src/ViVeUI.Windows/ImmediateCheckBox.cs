using System.Windows;
using System.Windows.Controls;

namespace ViVeUI.Windows;

// Mouse, Space and UI Automation all enter OnToggle. Keep IsChecked bound to
// verified storage state instead of displaying an optimistic local toggle.
public sealed class ImmediateCheckBox : CheckBox
{
    public event RoutedEventHandler? ToggleRequested;
    protected override void OnToggle() => ToggleRequested?.Invoke(this, new RoutedEventArgs());
}
