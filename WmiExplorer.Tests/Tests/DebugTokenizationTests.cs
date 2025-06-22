using ICSharpCode.AvalonEdit.Document;
using NUnit.Framework;
using WmiExplorer.Integration.AvalonEdit.Context;
using WmiExplorer.Integration.AvalonEdit.WqlManager;

namespace WmiExplorer.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class DebugTokenizationTests
{
    [Test]
    public async Task TestCompletionHelper()
    {
        // Test the actual completion helper method
        var editor = Helpers.CompletionTestHelper.CreateTestEditor("", (int?)null);
        var mockCache = new Mocks.MockCacheService();

        var completions = await Helpers.CompletionTestHelper.GetCompletionsAsync(editor, mockCache);
        Console.WriteLine($"Completions count: {completions.Count}");
        foreach (var completion in completions)
        {
            Console.WriteLine($"Completion: '{completion.Text}'");
        }
    }

    [Test]
    public async Task TestDirectProviderCall()
    {
        // Test calling the provider directly
        var context = QueryContext.Analyze(new TextDocument(""), 0);
        Console.WriteLine($"Direct context: {context?.ContextType}");

        if (context != null)
        {
            var provider = new WmiExplorer.Integration.AvalonEdit.Providers.KeywordCompletionProvider();
            var canProvide = provider.CanProvideCompletion(context);
            Console.WriteLine($"Can provide: {canProvide}");

            if (canProvide)
            {
                var mockCache = new Mocks.MockCacheService();
                var completions = await provider.GetCompletionDataAsync(context, "", mockCache, "root\\CIMV2");
                Console.WriteLine($"Direct completions count: {completions.Count}");
                foreach (var completion in completions)
                {
                    Console.WriteLine($"Direct completion: '{completion.Text}'");
                }
            }
        }
    }

    [Test]
    public void TestEmptyStringTokenization()
    {
        var tokens = WqlTokenizer.TokenizeToPosition("", 0);
        Console.WriteLine($"Empty string tokens count: {tokens.Count}");

        var context = QueryContext.Analyze(new TextDocument(""), 0);
        Console.WriteLine($"Context: {context?.ContextType}");
        Console.WriteLine($"Context is null: {context == null}");
    }

    [Test]
    public void TestSelectSpaceTokenization()
    {
        var tokens = WqlTokenizer.TokenizeToPosition("SELECT ", 7);
        Console.WriteLine($"'SELECT ' tokens count: {tokens.Count}");
        foreach (var token in tokens)
        {
            Console.WriteLine($"Token: '{token.Text}', Type: {token.Type}, Start: {token.StartIndex}");
        }

        var context = QueryContext.Analyze(new TextDocument("SELECT "), 7);
        Console.WriteLine($"Context: {context?.ContextType}");
        Console.WriteLine($"Context is null: {context == null}");
    }
}