using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace WmiExplorer.PropertyGrid.Converters;

/// Converter that creates highlighted text runs for search terms within text
/// </summary>
public class TextHighlightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            // Handle null or insufficient values safely
            if (values == null || values.Length < 2)
            {
                string fallbackText = values?[0]?.ToString() ?? string.Empty;
                return new TextBlock { Text = fallbackText };
            }
            string text = values[0]?.ToString() ?? string.Empty;
            string searchTerm = values[1]?.ToString() ?? string.Empty;

            var textBlock = new TextBlock();

            // Apply style based on parameter (for category vs property styling)
            if (parameter is string styleKey && !string.IsNullOrEmpty(styleKey))
            {
                try
                {
                    if (Application.Current?.TryFindResource(styleKey) is Style style)
                    {
                        textBlock.Style = style;
                    }
                }
                catch
                {
                    // Ignore style application errors
                }
            }

            // Early return for empty search term - no highlighting needed
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                textBlock.Text = text;
                return textBlock;
            }

            // Early return for empty text
            if (string.IsNullOrEmpty(text))
            {
                return textBlock;
            }

            // Find all matches (case-insensitive)
            var matches = FindMatches(text, searchTerm);

            if (matches.Count == 0)
            {
                textBlock.Text = text;
                return textBlock;
            }

            // Build runs with highlighting
            int currentIndex = 0;
            foreach (var match in matches)
            {
                // Add text before the match
                if (match.Start > currentIndex)
                {
                    textBlock.Inlines.Add(new Run(text.Substring(currentIndex, match.Start - currentIndex)));
                }

                // Add highlighted match
                var highlightRun = new Run(text.Substring(match.Start, match.Length));
                try
                {
                    highlightRun.Background = (Brush?)Application.Current?.TryFindResource("PropertyGridSelectedBackgroundBrush")
                        ?? new SolidColorBrush(Color.FromRgb(255, 255, 0)); // Yellow fallback
                    highlightRun.Foreground = (Brush?)Application.Current?.TryFindResource("PropertyGridForegroundBrush")
                        ?? new SolidColorBrush(Colors.Black); // Black fallback
                }
                catch
                {
                    // Use fallback colors if resource lookup fails
                    highlightRun.Background = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                    highlightRun.Foreground = new SolidColorBrush(Colors.Black);
                }

                textBlock.Inlines.Add(highlightRun);

                currentIndex = match.Start + match.Length;
            }

            // Add remaining text after last match
            if (currentIndex < text.Length)
            {
                textBlock.Inlines.Add(new Run(text.Substring(currentIndex)));
            }

            return textBlock;
        }
        catch (Exception)
        {
            // If anything goes wrong, return a simple TextBlock with the original text
            string fallbackText = values?[0]?.ToString() ?? string.Empty;
            return new TextBlock { Text = fallbackText };
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static System.Collections.Generic.List<MatchInfo> FindMatches(string text, string searchTerm)
    {
        var matches = new System.Collections.Generic.List<MatchInfo>();
        int index = 0;

        while (index < text.Length)
        {
            int foundIndex = text.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase);
            if (foundIndex == -1)
                break;

            matches.Add(new MatchInfo { Start = foundIndex, Length = searchTerm.Length });
            index = foundIndex + searchTerm.Length;
        }

        return matches;
    }

    private struct MatchInfo
    {
        public int Length { get; set; }
        public int Start { get; set; }
    }
}