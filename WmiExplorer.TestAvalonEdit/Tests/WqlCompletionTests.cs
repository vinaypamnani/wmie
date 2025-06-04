using NUnit.Framework;
using WmiExplorer.Presentation.AvalonEdit.WqlManager;
using WmiExplorer.TestAvalonEdit.Helpers;
using WmiExplorer.TestAvalonEdit.Mocks;

namespace WmiExplorer.TestAvalonEdit.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class WqlCompletionTests
{
    private MockCacheService _mockCache = null!;

    public static IEnumerable<CompletionTestCase> CompletionTestCases()
    {
        yield return new CompletionTestCase
        {
            Query = "",
            ExpectedCompletions = new[] { WqlKeywordManager.Select },
            UnexpectedCompletions = Array.Empty<string>()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT ",
            ExpectedCompletions = new[] { WqlKeywordManager.Star },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.Except([WqlKeywordManager.Star]).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * ",
            ExpectedCompletions = new[] { WqlKeywordManager.From },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.Except([WqlKeywordManager.From]).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * FROM ",
            ExpectedCompletions = new[] { "Win32_Process", "Win32_OperatingSystem", "Win32_Service" },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * FROM Win32_Process ",
            ExpectedCompletions = new[] { WqlKeywordManager.Where },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.Except([WqlKeywordManager.Where]).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT  FROM Win32_Process ",
            CaretPosition = 7,
            ExpectedCompletions = new[] { WqlKeywordManager.Star, "Name", "ProcessId", "ExecutablePath", "CommandLine" },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.Except([WqlKeywordManager.Star]).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT Name,  FROM Win32_Process ",
            CaretPosition = 13,
            ExpectedCompletions = new[] { "ProcessId", "ExecutablePath", "CommandLine" },
            UnexpectedCompletions = (new[] { "Name" }).Concat(WqlKeywordManager.AllKeywords).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * FROM Win32_Process WHERE ",
            ExpectedCompletions = new[] { "Name", "ProcessId", "ExecutablePath", "CommandLine", WqlKeywordManager.Not },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.Except([WqlKeywordManager.Not]).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * FROM Win32_Process WHERE Name ",
            ExpectedCompletions = WqlKeywordManager.GetComparisonOperators().ToArray(),
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.Except(WqlKeywordManager.GetComparisonOperators()).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * FROM Win32_Process WHERE Name = ",
            ExpectedCompletions = Array.Empty<string>(),
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' ",
            ExpectedCompletions = new[] { WqlKeywordManager.And, WqlKeywordManager.Or },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.Except([WqlKeywordManager.And, WqlKeywordManager.Or]).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ",
            ExpectedCompletions = new[] { "Name", "ProcessId", "ExecutablePath", "CommandLine", WqlKeywordManager.Not },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.Except([WqlKeywordManager.Not]).ToArray()
        };

        yield return new CompletionTestCase
        {
            Query = "SELECT * FROM Win32_Process WHERE NOT ",
            ExpectedCompletions = new[] { "Name", "ProcessId", "ExecutablePath", "CommandLine" },
            UnexpectedCompletions = WqlKeywordManager.AllKeywords.ToArray()
        };
    }

    [SetUp]
    public void Setup()
    {
        _mockCache = new MockCacheService();
    }

    [Test, TestCaseSource(nameof(CompletionTestCases))]
    public async Task WqlCompletionTest(CompletionTestCase testCase)
    {
        // Arrange
        var editor = CompletionTestHelper.CreateTestEditor(testCase.Query, testCase.CaretPosition);

        // Act
        var completions = await CompletionTestHelper.GetCompletionsAsync(editor, _mockCache);
        var completionTexts = completions.Select(c => c.Text).ToList();

        // Assert - Check expected completions
        foreach (var expected in testCase.ExpectedCompletions)
        {
            Assert.That(completionTexts, Contains.Item(expected),
                $"Should offer '{expected}' for '{testCase.Query}' but found: [{string.Join(", ", completionTexts)}]");
        }

        // Assert - Check unexpected completions
        foreach (var unexpected in testCase.UnexpectedCompletions)
        {
            Assert.That(completionTexts, Does.Not.Contain(unexpected),
                $"Should NOT offer '{unexpected}' for '{testCase.Query}' but it was found");
        }

        // Assert - Check total count matches (if we specified all expected values)
        if (testCase.UnexpectedCompletions.Length == 0)
        {
            Assert.That(completions, Has.Count.EqualTo(testCase.ExpectedCompletions.Length),
                $"Expected {testCase.ExpectedCompletions.Length} completions for '{testCase.Query}' but found {completions.Count}: [{string.Join(", ", completionTexts)}]");
        }
    }

    public class CompletionTestCase
    {
        public int? CaretPosition { get; set; }
        public string[] ExpectedCompletions { get; set; } = Array.Empty<string>();
        public string Query { get; set; } = string.Empty;
        public string[] UnexpectedCompletions { get; set; } = Array.Empty<string>();

        public override string ToString()
        {
            return $"Query: [{Query}], Caret: {CaretPosition}";
        }
    }
}