using System.Windows.Media;
using ViVeUI.Core;
namespace ViVeUI.Windows;
public sealed record FeatureCard(Feature Feature, string Title, string Description, string Category, ImageSource? Image, string Context, string DetailLabel, bool IsSelected, string HistoryLabel)
{
    public uint Id => Feature.Id;
}
public sealed record ReviewCard(uint Id, string Title, string TechnicalName, string Description, ImageSource? Image, string Before, string After, string Context, string BeforeLabel, string AfterLabel, string DetailsLabel, string RemoveLabel, string CopyLabel);
public static class FeatureEditorial
{
    public static string? Key(uint id) => id switch { 37634385 => "Tabs", 39420424 => "Search", 34300186 => "Widgets", 36354489 => "Navigation", _ => null };
    public static string Title(Feature feature, Locale locale) => Key(feature.Id) is string key ? locale[key + "Title"] : feature.Name;
    public static string Description(Feature feature, Locale locale) => Key(feature.Id) is string key ? locale[key + "Description"] : locale["UnknownDescription"];
}
