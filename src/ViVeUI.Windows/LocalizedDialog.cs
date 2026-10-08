using System.Windows;
using System.Windows.Controls;

namespace ViVeUI.Windows;
// Native WPF buttons use the selected UI language, rather than the OS MessageBox language.
// Cancel remains the default for every privileged confirmation.
internal static class LocalizedDialog
{
    internal static Window Create(Window? owner, Locale locale, string title, string body, bool confirm = false, string? technical = null, string? scope = null)
    {
        var dialog = new Window { Title = title, Width = 640, MaxHeight = 720, SizeToContent = SizeToContent.Height, MinWidth = 420,
            FlowDirection = locale.Direction, FontFamily = locale.Font, Language = locale.XmlLanguage,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize, Background = SystemColors.WindowBrush, Foreground = SystemColors.WindowTextBrush };
        if (owner is not null) dialog.Owner = owner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        var text = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, FontSize = 15 };
        var content = new StackPanel(); content.Children.Add(text);
        if (scope is not null)
        {
            var exactScope = new TextBlock { Text = scope, FlowDirection = FlowDirection.LeftToRight, TextWrapping = TextWrapping.Wrap, FontSize = 15, Margin = new Thickness(0,16,0,0) };
            System.Windows.Media.NumberSubstitution.SetSubstitution(exactScope, System.Windows.Media.NumberSubstitutionMethod.European);
            content.Children.Add(exactScope);
        }
        panel.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 410 });
        if (!string.IsNullOrEmpty(technical)) panel.Children.Add(new Expander { Header = locale["Diagnostics"], Margin = new Thickness(0,16,0,0), Content = new TextBox { Text = technical, IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, TextWrapping = TextWrapping.Wrap, MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,22,0,0) };
        if (confirm)
        {
            var accept = new Button { Content = locale["Confirm"], MinWidth = 100, MaxWidth = 230, Margin = new Thickness(0,0,12,0) };
            accept.Click += (_, _) => dialog.DialogResult = true; buttons.Children.Add(accept);
        }
        var cancel = new Button { Content = locale[confirm ? "Cancel" : "Close"], MinWidth = 100, MaxWidth = 230, IsCancel = true, IsDefault = true };
        cancel.Click += (_, _) => dialog.DialogResult = false; buttons.Children.Add(cancel); panel.Children.Add(buttons);
        dialog.Content = panel; return dialog;
    }
    public static bool Show(Window? owner, Locale locale, string title, string body, bool confirm = false, string? technical = null, string? scope = null) => Create(owner, locale, title, body, confirm, technical, scope).ShowDialog() == true;
}
