using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WmiExplorer.Presentation.Behaviors.AvalonEdit;

/// <summary>
/// Represents a completion data item for IntelliSense-like functionality.
/// </summary>
public class SimpleCompletionData : ICompletionData
{
    public enum CompletionType
    {
        Keyword,
        Property,
        Class,
        Special,
        Operator
    }

    private readonly string _description;
    private readonly string _detailedType;
    private readonly CompletionType _type;
    private static readonly ImageSource ClassIcon;
    private static readonly ImageSource KeywordIcon;
    private static readonly ImageSource OperatorIcon;
    private static readonly ImageSource PropertyIcon;
    private static readonly ImageSource SpecialIcon;

    public SimpleCompletionData(string text, CompletionType type = CompletionType.Keyword, string description = "", string detailedType = "")
    {
        Text = text;
        _type = type;
        _description = description;
        _detailedType = detailedType;
    }

    static SimpleCompletionData()
    {
        // Create stylized icons for each type of completion item
        var keywordBrush = new SolidColorBrush(Colors.RoyalBlue);
        var propertyBrush = new SolidColorBrush(Colors.ForestGreen);
        var classBrush = new SolidColorBrush(Colors.Orange);
        var specialBrush = new SolidColorBrush(Colors.Purple);
        var operatorBrush = new SolidColorBrush(Colors.DarkRed);

        keywordBrush.Freeze();
        propertyBrush.Freeze();
        classBrush.Freeze();
        specialBrush.Freeze();
        operatorBrush.Freeze();

        KeywordIcon = CreateIconDrawing("K", keywordBrush);
        PropertyIcon = CreateIconDrawing("P", propertyBrush);
        ClassIcon = CreateIconDrawing("C", classBrush);
        SpecialIcon = CreateIconDrawing("*", specialBrush);
        OperatorIcon = CreateIconDrawing("O", operatorBrush);
    }

    public object Content
    {
        get
        {
            // Create a TextBlock for more customized display
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = Text,
                FontWeight = _type == CompletionType.Keyword ? FontWeights.Bold : FontWeights.Normal,
                Margin = new Thickness(6, 0, 0, 0) // Add space between icon and text
            };

            return textBlock;
        }
    }

    public object Description
    {
        get
        {
            if (string.IsNullOrEmpty(_description))
            {
                return $"{_type}: {Text}";
            }

            // For properties with type information, create a more detailed description
            if (_type == CompletionType.Property && !string.IsNullOrEmpty(_detailedType))
            {
                var panel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

                var nameBlock = new System.Windows.Controls.TextBlock
                {
                    Text = _description,
                    FontWeight = FontWeights.Bold
                };

                var typeBlock = new System.Windows.Controls.TextBlock
                {
                    Text = _detailedType,
                    Foreground = Brushes.DarkGray,
                    Margin = new Thickness(0, 3, 0, 0)
                };

                panel.Children.Add(nameBlock);
                panel.Children.Add(typeBlock);

                return panel;
            }

            return _description;
        }
    }

    public ImageSource Image => _type switch
    {
        CompletionType.Keyword => KeywordIcon,
        CompletionType.Property => PropertyIcon,
        CompletionType.Class => ClassIcon,
        CompletionType.Special => SpecialIcon,
        CompletionType.Operator => OperatorIcon,
        _ => null!
    };

    public double Priority => _type switch
    {
        CompletionType.Special => 100,  // Special items like * should appear first
        CompletionType.Property => 80,   // Properties are next
        CompletionType.Class => 60,      // Then classes
        CompletionType.Keyword => 40,    // Then keywords
        CompletionType.Operator => 20,   // Then operators
        _ => 0
    };

    public string Text { get; private set; }

    public void Complete(TextArea textArea, ISegment segment, EventArgs insertionRequestEventArgs)
    {
        var document = textArea.Document;
        int caretOffset = textArea.Caret.Offset;

        int wordStart = TextUtilities.GetNextCaretPosition(document, caretOffset,
            LogicalDirection.Backward, CaretPositioningMode.WordStart);
        int wordEnd = TextUtilities.GetNextCaretPosition(document, caretOffset,
            LogicalDirection.Forward, CaretPositioningMode.WordBorder);

        int startOffset = wordStart >= 0 ? wordStart : segment.Offset;
        int endOffset = wordEnd >= 0 ? wordEnd : segment.EndOffset;

        // Determine if caret is after a word character (letter, digit, underscore)
        bool afterWordChar = caretOffset > 0 && (char.IsLetterOrDigit(document.GetCharAt(caretOffset - 1)) || document.GetCharAt(caretOffset - 1) == '_');
        bool atEndOfDoc = caretOffset == document.TextLength;

        string textToInsert = Text;
        if (_type == CompletionType.Class || _type == CompletionType.Property)
        {
            if (Text.Contains(' ') && !Text.StartsWith("`"))
            {
                textToInsert = $"`{Text}`";
            }
        }

        // Replace the word if after a word character, otherwise insert at caret
        if (afterWordChar)
        {
            if (startOffset >= 0 && endOffset <= document.TextLength && endOffset > startOffset)
            {
                document.Replace(startOffset, endOffset - startOffset, textToInsert);
                textArea.Caret.Offset = startOffset + textToInsert.Length;
            }
        }
        else
        {
            document.Insert(caretOffset, textToInsert);
            textArea.Caret.Offset = caretOffset + textToInsert.Length;
        }

        // Do not add a space after any completion (keywords, classes, properties, etc.)
        // Special handling for certain completions:
        // 1. After selecting FROM or WHERE, position caret for class name and trigger completion window
        if (_type == CompletionType.Keyword &&
            (Text.Equals("FROM", StringComparison.OrdinalIgnoreCase) ||
             Text.Equals("WHERE", StringComparison.OrdinalIgnoreCase)))
        {
            // --- Trigger completion window for property/class suggestions ---
            var editor = AvalonEditCompletionBehavior.FindParentTextEditor(textArea);
            if (editor != null)
            {
                _ = editor.Dispatcher.InvokeAsync(async () =>
                {
                    AvalonEditCompletionBehavior.LastCompletionTime[editor] = DateTime.UtcNow;
                    await AvalonEditCompletionBehavior.ShowCompletionWindow(textArea, editor, false);
                });
            }
        }
    }

    private static ImageSource CreateIconDrawing(string text, Brush brush)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            // Draw a rounded rectangle as background
            var bgRect = new Rect(0, 0, 16, 16);
            var bgGeometry = new RectangleGeometry(bgRect, 2, 2);
            context.DrawGeometry(brush, new Pen(Brushes.Gray, 0.5), bgGeometry);

            // Draw the text
            var textGeometry = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11,
                Brushes.White,
                VisualTreeHelper.GetDpi(new System.Windows.Controls.Image()).PixelsPerDip);

            // Center the text in the icon
            Point centerPoint = new Point(
                (bgRect.Width - textGeometry.Width) / 2,
                (bgRect.Height - textGeometry.Height) / 2);

            context.DrawText(textGeometry, centerPoint);
        }

        var drawingImage = new DrawingImage(drawing);
        drawingImage.Freeze(); // For better performance
        return drawingImage;
    }
}