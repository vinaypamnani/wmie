using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using WmiExplorer.Services;
using System.Reflection;
using WmiExplorer.Presentation.AvalonEdit.Behaviors;

namespace WmiExplorer.TestAvalonEdit.Helpers;

/// <summary>
/// Provides helper methods for testing AvalonEdit completions
/// </summary>
public static class CompletionTestHelper
{
    /// <summary>
    /// Creates a TextEditor for testing with the specified query text
    /// </summary>
    public static TextEditor CreateTestEditor(string queryText, string namespacePath = "root\\CIMV2")
    {
        var editor = new TextEditor
        {
            Document = new TextDocument(queryText)
        };

        // Set the caret at the end of the document
        editor.CaretOffset = queryText.Length;

        // Set the namespace and enable completion
        AvalonEditCompletionBehavior.SetNamespace(editor, namespacePath);
        AvalonEditCompletionBehavior.SetEnableCompletion(editor, true);

        return editor;
    }

    /// <summary>
    /// Creates a TextEditor for testing with the specified query text and optional caret position
    /// </summary>
    public static TextEditor CreateTestEditor(string queryText, int? caretPosition = null)
    {
        var editor = new TextEditor
        {
            Document = new TextDocument(queryText)
        };

        // Set the caret at the specified position or at the end of the document
        editor.CaretOffset = caretPosition ?? queryText.Length;

        // Default namespace path
        string namespacePath = "root\\CIMV2";

        // Set the namespace and enable completion
        AvalonEditCompletionBehavior.SetNamespace(editor, namespacePath);
        AvalonEditCompletionBehavior.SetEnableCompletion(editor, true);

        return editor;
    }

    /// <summary>
    /// Gets the list of completions for the current state of the editor
    /// </summary>
    public static async Task<List<ICompletionData>> GetCompletionsAsync(
        TextEditor editor,
        ICacheService cacheService,
        bool forceShow = true)
    {
        // Set the cache service
        AvalonEditCompletionBehavior.SetCacheService(editor, cacheService);

        // Create a context using reflection since QueryContext is internal
        var queryContextType = Type.GetType("WmiExplorer.Presentation.AvalonEdit.Context.QueryContext, WmiExplorer");

        if (queryContextType == null)
            throw new InvalidOperationException("Could not find QueryContext type through reflection");

        var analyzeMethod = queryContextType.GetMethod("Analyze",
            BindingFlags.Public | BindingFlags.Static);

        if (analyzeMethod == null)
            throw new InvalidOperationException("Could not find Analyze method through reflection");

        var context = analyzeMethod.Invoke(null, new object[] { editor.Document, editor.CaretOffset });

        // Use reflection to access the private method for getting completions
        var collectCompletionsMethod = typeof(AvalonEditCompletionBehavior).GetMethod(
            "CollectCompletionsFromProviders",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (collectCompletionsMethod == null)
            throw new InvalidOperationException(
                "Could not find CollectCompletionsFromProviders method through reflection");

        // Call the method
        var result = await (Task<List<ICompletionData>>)collectCompletionsMethod.Invoke(
            null,
            new object?[]
            {
                    context,
                    GetWordAtCursor(editor),
                    cacheService,
                    AvalonEditCompletionBehavior.GetNamespace(editor)
            })!;

        return result;
    }

    /// <summary>
    /// Gets the current word at the caret position
    /// </summary>
    private static string GetWordAtCursor(TextEditor editor)
    {
        int caretOffset = editor.CaretOffset;
        var document = editor.Document;

        if (caretOffset <= 0 || caretOffset > document.TextLength)
            return string.Empty;

        // Find word boundaries
        int wordStart = caretOffset;
        while (wordStart > 0)
        {
            char ch = document.GetCharAt(wordStart - 1);
            if (!char.IsLetterOrDigit(ch) && ch != '_')
                break;
            wordStart--;
        }

        string prefix = wordStart < caretOffset
            ? document.GetText(wordStart, caretOffset - wordStart)
            : string.Empty;

        return prefix;
    }
}