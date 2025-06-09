using ICSharpCode.AvalonEdit.Document;
using NUnit.Framework;
using WmiExplorer.Integration.AvalonEdit.Context;

namespace WmiExplorer.TestAvalonEdit.Tests;

[TestFixture]
public class WqlTokenizerTests
{
    public static IEnumerable<TokenizerTestCase> TokenizerTestCases()
    {
        yield return new TokenizerTestCase { Query = "", ExpectedContextType = QueryContext.ContextKind.StartQuery };
        yield return new TokenizerTestCase { Query = "SELECT", ExpectedContextType = QueryContext.ContextKind.AfterSelect };
        yield return new TokenizerTestCase { Query = "SELECT * ", ExpectedContextType = QueryContext.ContextKind.AfterStar };
        yield return new TokenizerTestCase { Query = "SELECT * FROM ", ExpectedContextType = QueryContext.ContextKind.AfterFrom };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process ", ExpectedContextType = QueryContext.ContextKind.AfterClass };
        yield return new TokenizerTestCase { Query = "SELECT  FROM Win32_Process ", CaretPosition = 7, ExpectedContextType = QueryContext.ContextKind.AfterSelect };
        yield return new TokenizerTestCase { Query = "SELECT Name,  FROM Win32_Process ", CaretPosition = 13, ExpectedContextType = QueryContext.ContextKind.SelectProps };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE ", ExpectedContextType = QueryContext.ContextKind.AfterWhere };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name ", ExpectedContextType = QueryContext.ContextKind.AfterProperty };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe'", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ", ExpectedContextType = QueryContext.ContextKind.AfterLogicalOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ProcessId ", ExpectedContextType = QueryContext.ContextKind.AfterProperty };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ProcessId > ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ProcessId > 1234 ", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT ", ExpectedContextType = QueryContext.ContextKind.AfterNot };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT Name ", ExpectedContextType = QueryContext.ContextKind.AfterProperty };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT Name = ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT Name = 'notepad.exe'", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT Name = 'notepad.exe' AND ", ExpectedContextType = QueryContext.ContextKind.AfterLogicalOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name IS ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name IS NULL ", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name IS NOT NULL ", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name LIKE ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'xy", ExpectedContextType = QueryContext.ContextKind.InValue };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name <> 'xy", ExpectedContextType = QueryContext.ContextKind.InValue };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE ProcessId > 1234", ExpectedContextType = QueryContext.ContextKind.InValue };
        yield return new TokenizerTestCase { Query = "SELECT Name, ProcessId FROM Win32_Process WHERE Name = 'notepad.exe' ", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT Name, ProcessId FROM Win32_Process WHERE ( ", ExpectedContextType = QueryContext.ContextKind.AfterOpenParenthesis };
        yield return new TokenizerTestCase { Query = "SELECT Name, ProcessId FROM Win32_Process WHERE (Name = 'notepad.exe') ", ExpectedContextType = QueryContext.ContextKind.AfterCloseParenthesis };
    }

    [Test, TestCaseSource(nameof(TokenizerTestCases))]
    public void WqlTokenizerTest(TokenizerTestCase testCase)
    {
        // Test context analysis
        var document = new TextDocument(testCase.Query);

        // Use specified caret position or default to end of query
        int caretPosition = testCase.CaretPosition ?? testCase.Query.Length;

        var context = QueryContext.Analyze(document, caretPosition);

        // Assert context type matches expected
        Assert.That(context?.ContextType, Is.EqualTo(testCase.ExpectedContextType),
            $"ContextType mismatch for query: [{testCase.Query}], Context.LastTokenText: {context?.LastTokenText}, Context.LastSignificantTokenText: {context?.LastSignificantToken?.Text}, Context.LastSignificantTokenType: {context?.LastSignificantToken?.Type}, Context.ClassName: {context?.ClassName}");
    }

    public class TokenizerTestCase
    {
        public string Query { get; set; } = string.Empty;
        internal QueryContext.ContextKind ExpectedContextType { get; set; }

        /// <summary>
        /// Optional caret position for context analysis. If not set, defaults to end of query.
        /// </summary>
        public int? CaretPosition { get; set; }

        public override string ToString()
        {
            return $"Query: [{Query}], ExpectedContextType: {ExpectedContextType}, CaretPosition: {CaretPosition}";
        }
    }
}