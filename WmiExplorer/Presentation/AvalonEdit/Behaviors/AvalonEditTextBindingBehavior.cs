using ICSharpCode.AvalonEdit;
using System.Windows;

namespace WmiExplorer.Presentation.AvalonEdit.Behaviors;

/// <summary>
/// Enables two-way binding between AvalonEdit's Text property and a ViewModel string property using attached property.
/// </summary>
public static class AvalonEditTextBindingBehavior
{
    public static readonly DependencyProperty BoundTextProperty = DependencyProperty.RegisterAttached(
        "BoundText",
        typeof(string),
        typeof(AvalonEditTextBindingBehavior),
        new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundTextChanged));

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating",
        typeof(bool),
        typeof(AvalonEditTextBindingBehavior),
        new PropertyMetadata(false));

    public static string GetBoundText(DependencyObject obj) => (string)obj.GetValue(BoundTextProperty);

    public static void SetBoundText(DependencyObject obj, string value) => obj.SetValue(BoundTextProperty, value);

    private static void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (sender is TextEditor editor)
        {
            if (GetIsUpdating(editor)) return;
            SetIsUpdating(editor, true);
            SetBoundText(editor, editor.Text);
            SetIsUpdating(editor, false);
        }
    }

    private static bool GetIsUpdating(DependencyObject obj) => (bool)obj.GetValue(IsUpdatingProperty);

    private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextEditor editor)
        {
            // Detach previous handler if any
            editor.TextChanged -= Editor_TextChanged;
            editor.TextChanged += Editor_TextChanged;

            if (GetIsUpdating(editor)) return;
            SetIsUpdating(editor, true);
            if (editor.Text != (e.NewValue as string))
                editor.Text = e.NewValue as string ?? string.Empty;
            SetIsUpdating(editor, false);
        }
    }

    private static void SetIsUpdating(DependencyObject obj, bool value) => obj.SetValue(IsUpdatingProperty, value);
}