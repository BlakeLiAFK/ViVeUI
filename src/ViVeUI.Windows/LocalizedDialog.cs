using System.Windows;
using System.Windows.Controls;

namespace ViVeUI.Windows;
// Native WPF buttons use the selected UI language, rather than the OS MessageBox language.
// Cancel remains the default for every privileged confirmation.
internal static class LocalizedDialog
{
    internal static Window Create(Window? owner, Locale locale, string title, string body, bool confirm = false, string? technical = null, IReadOnlyList<ViVeUI.Core.Change>? scope = null)
    {
        var dialog = new Window { Icon = owner?.Icon ?? IconResources.Load(), Title = title, Width = 640, MaxHeight = 720, SizeToContent = SizeToContent.Height, MinWidth = 420,
            FlowDirection = locale.Direction, FontFamily = locale.Font, Language = locale.XmlLanguage,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize, Background = SystemColors.WindowBrush, Foreground = SystemColors.WindowTextBrush };
        if (owner is not null) dialog.Owner = owner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        var text = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, FontSize = 15 };
        var content = new StackPanel(); content.Children.Add(text);
        if (scope is not null)
        {
            // Separate bidi paragraphs keep each ID and before/after value unambiguous.
            var exactScope = new Grid { FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0,16,0,0) };
            exactScope.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            exactScope.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            exactScope.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            void Cell(string value, int row, int column, bool heading = false)
            {
                var cell = new TextBlock { Text = value, FlowDirection = column == 0 ? FlowDirection.LeftToRight : locale.Direction,
                    TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0,4,12,4),
                    FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal };
                System.Windows.Media.NumberSubstitution.SetSubstitution(cell, System.Windows.Media.NumberSubstitutionMethod.European);
                Grid.SetRow(cell,row); Grid.SetColumn(cell,column); exactScope.Children.Add(cell);
            }
            exactScope.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Cell("ID",0,0,true); Cell(locale["BeforeLabel"],0,1,true); Cell(locale["AfterLabel"],0,2,true);
            for (var i = 0; i < scope.Count; i++)
            {
                exactScope.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Cell(Localization.FeatureId(scope[i].Id),i+1,0); Cell(locale.State(scope[i].Before),i+1,1); Cell(locale.State(scope[i].After),i+1,2);
            }
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
    public static bool Show(Window? owner, Locale locale, string title, string body, bool confirm = false, string? technical = null, IReadOnlyList<ViVeUI.Core.Change>? scope = null) => Create(owner, locale, title, body, confirm, technical, scope).ShowDialog() == true;
}
