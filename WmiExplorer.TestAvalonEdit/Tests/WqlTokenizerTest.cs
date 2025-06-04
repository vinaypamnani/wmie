using ICSharpCode.AvalonEdit.Document;
using NUnit.Framework;
using WmiExplorer.Presentation.AvalonEdit.Context;

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
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE ", ExpectedContextType = QueryContext.ContextKind.AfterWhere };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name ", ExpectedContextType = QueryContext.ContextKind.AfterProperty };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe'", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ", ExpectedContextType = QueryContext.ContextKind.AfterLogicalOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ProcessId ", ExpectedContextType = QueryContext.ContextKind.AfterProperty };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ProcessId > ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ProcessId > 1234", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT ", ExpectedContextType = QueryContext.ContextKind.AfterNot };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT Name ", ExpectedContextType = QueryContext.ContextKind.AfterProperty };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT Name = ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT Name = 'notepad.exe'", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE NOT Name = 'notepad.exe' AND ", ExpectedContextType = QueryContext.ContextKind.AfterLogicalOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name IS ", ExpectedContextType = QueryContext.ContextKind.AfterOperator };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name IS NULL ", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name IS NOT NULL ", ExpectedContextType = QueryContext.ContextKind.AfterCompleteCondition };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name = 'xy", ExpectedContextType = QueryContext.ContextKind.InValue };
        yield return new TokenizerTestCase { Query = "SELECT * FROM Win32_Process WHERE Name <> 'xy", ExpectedContextType = QueryContext.ContextKind.InValue };
    }

    [Test, TestCaseSource(nameof(TokenizerTestCases))]
    public void WqlTokenizerTest(TokenizerTestCase testCase)
    {
        // Test context analysis
        var document = new TextDocument(testCase.Query);
        var context = QueryContext.Analyze(document, testCase.Query.Length);

        // Assert context type matches expected
        Assert.That(context?.ContextType, Is.EqualTo(testCase.ExpectedContextType), $"ContextType mismatch for query: [{testCase.Query}]");
    }

    public class TokenizerTestCase
    {
        public string Query { get; set; } = string.Empty;
        internal QueryContext.ContextKind ExpectedContextType { get; set; }

        public override string ToString()
        {
            return $"Query: '{Query}', ExpectedContextType: {ExpectedContextType}";
        }
    }
}