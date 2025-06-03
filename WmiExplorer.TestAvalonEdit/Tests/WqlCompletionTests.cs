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

    public static IEnumerable<TestCaseData> CompletionTestCases()
    {
        // Test case format: (query, expected completions, unexpected completions)
        yield return new TestCaseData(
            "",
            new[] { WqlKeywordManager.Select },
            new string[0]
        ).SetName("EmptyQuery_ShouldOfferSELECT");

        yield return new TestCaseData(
            "SELECT ",
            new[] { WqlKeywordManager.Star },
            new[] { WqlKeywordManager.From, WqlKeywordManager.Where, WqlKeywordManager.Select }
        ).SetName("SelectQuery_ShouldOfferAsteriskOnly");

        yield return new TestCaseData(
            "SELECT * ",
            new[] { WqlKeywordManager.From },
            new[] { WqlKeywordManager.Where, WqlKeywordManager.Select }
        ).SetName("SelectAsterisk_ShouldOfferFROMOnly");

        yield return new TestCaseData(
            "SELECT * FROM ",
            new[] { "Win32_Process", "Win32_OperatingSystem", "Win32_Service" },
            new[] { WqlKeywordManager.Where }
        ).SetName("SelectFrom_ShouldOfferClassListOnly");

        yield return new TestCaseData(
            "SELECT * FROM Win32_Process ",
            new[] { WqlKeywordManager.Where },
            new[] { WqlKeywordManager.Select, WqlKeywordManager.From }
        ).SetName("SelectFromClass_ShouldOfferWHEREOnly");

        yield return new TestCaseData(
            "SELECT * FROM Win32_Process WHERE ",
            new[] { "Name", "ProcessId", "ExecutablePath", "CommandLine", WqlKeywordManager.Not },
            new[] { WqlKeywordManager.Select, WqlKeywordManager.From, WqlKeywordManager.Where }
        ).SetName("SelectWhere_ShouldOfferPropertiesAndNOTOperator");

        yield return new TestCaseData(
            "SELECT * FROM Win32_Process WHERE Name ",
            WqlKeywordManager.GetComparisonOperators().ToArray(),
            new[] { WqlKeywordManager.Not, WqlKeywordManager.And, WqlKeywordManager.Or }
        ).SetName("SelectWhereName_ShouldOfferComparisonOperators");

        yield return new TestCaseData(
            "SELECT * FROM Win32_Process WHERE Name = ",
            new string[0],
            new[] { WqlKeywordManager.Not, WqlKeywordManager.And, WqlKeywordManager.Or, WqlKeywordManager.True, WqlKeywordManager.Null }
        ).SetName("SelectWhereNameEquals_ShouldOfferNoCompletions");

        yield return new TestCaseData(
            "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' ",
            new[] { WqlKeywordManager.And, WqlKeywordManager.Or },
            new[] { WqlKeywordManager.Not, WqlKeywordManager.True, WqlKeywordManager.Null }
        ).SetName("SelectWhereNameEqualsValue_ShouldOfferLogicalOperators");

        yield return new TestCaseData(
            "SELECT * FROM Win32_Process WHERE Name = 'notepad.exe' AND ",
            new[] { "Name", "ProcessId", "ExecutablePath", "CommandLine", WqlKeywordManager.Not },
            new[] { WqlKeywordManager.True, WqlKeywordManager.Null }
        ).SetName("SelectWhereNameEqualsValueAnd_ShouldOfferPropertiesAndNOT");

        yield return new TestCaseData(
            "SELECT * FROM Win32_Process WHERE NOT ",
            new[] { "Name", "ProcessId", "ExecutablePath", "CommandLine" },
            new[] { WqlKeywordManager.Not }
        ).SetName("SelectWhere_ShouldOfferPropertiesAfterNOT");
    }

    [SetUp]
    public void Setup()
    {
        _mockCache = new MockCacheService();
    }

    [Test, TestCaseSource(nameof(CompletionTestCases))]
    public async Task WqlCompletionTest(string query, string[] expectedCompletions, string[] unexpectedCompletions)
    {
        // Arrange
        var editor = CompletionTestHelper.CreateTestEditor(query);

        // Act
        var completions = await CompletionTestHelper.GetCompletionsAsync(editor, _mockCache);
        var completionTexts = completions.Select(c => c.Text).ToList();

        // Assert - Check expected completions
        foreach (var expected in expectedCompletions)
        {
            Assert.That(completionTexts, Contains.Item(expected),
                $"Should offer '{expected}' for '{query}' but found: [{string.Join(", ", completionTexts)}]");
        }

        // Assert - Check unexpected completions
        foreach (var unexpected in unexpectedCompletions)
        {
            Assert.That(completionTexts, Does.Not.Contain(unexpected),
                $"Should NOT offer '{unexpected}' for '{query}' but it was found");
        }

        // Assert - Check total count matches (if we specified all expected values)
        if (unexpectedCompletions.Length == 0)
        {
            Assert.That(completions, Has.Count.EqualTo(expectedCompletions.Length),
                $"Expected {expectedCompletions.Length} completions for '{query}' but found {completions.Count}: [{string.Join(", ", completionTexts)}]");
        }
    }
}