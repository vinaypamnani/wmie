using ICSharpCode.AvalonEdit.CodeCompletion;
using WmiExplorer.Presentation.AvalonEdit.Context;
using WmiExplorer.Presentation.AvalonEdit.WqlManager;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.AvalonEdit.Providers;

/// <summary>
/// Provides WMI property name completions after WHERE keyword or logical operators.
/// </summary>
internal class PropertyCompletionProvider : ICompletionProvider
{
    public bool CanProvideCompletion(QueryContext context)
    {
        return context.ContextType switch
        {
            QueryContext.ContextKind.AfterWhere => true,
            QueryContext.ContextKind.AfterLogicalOperator => true,
            QueryContext.ContextKind.AfterNot => true,
            _ => false
        };
    }

    public async Task<List<ICompletionData>> GetCompletionDataAsync(
        QueryContext context,
        string prefix,
        ICacheService? cacheService,
        string? namespacePath)
    {
        var completions = new List<ICompletionData>();

        if (!string.IsNullOrEmpty(context.ClassName) &&
            cacheService != null &&
            !string.IsNullOrEmpty(namespacePath))
        {
            await AddClassProperties(completions, cacheService, namespacePath, context.ClassName, prefix);
        }

        return completions;
    }

    private async Task AddClassProperties(
        List<ICompletionData> completions,
        ICacheService cacheService,
        string namespacePath,
        string className,
        string prefix)
    {
        try
        {
            var nsCache = await cacheService.GetNamespaceCacheAsync(namespacePath);
            if (nsCache?.Classes != null)
            {
                var classCache = nsCache.Classes.FirstOrDefault(c =>
                    c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));

                if (classCache?.Properties != null)
                {
                    foreach (var property in classCache.Properties)
                    {
                        if (string.IsNullOrEmpty(prefix) ||
                            property.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            string formattedType = FormatPropertyType(property.Type);
                            string description = BuildPropertyDescription(property, formattedType);

                            completions.Add(new WqlCompletionData(
                                property.Name,
                                CompletionType.Property,
                                description));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PropertyCompletionProvider] Error: {ex.Message}");
        }
    }

    private static string BuildPropertyDescription(dynamic property, string formattedType)
    {
        string description = $"Property: {property.Name}";

        if (!string.IsNullOrEmpty(formattedType))
            description += $" ({formattedType})";

        return description;
    }

    private static string FormatPropertyType(string wmiType)
    {
        return wmiType switch
        {
            "uint8" => "Byte",
            "sint8" => "SByte",
            "uint16" => "UInt16",
            "sint16" => "Int16",
            "uint32" => "UInt32",
            "sint32" => "Int32",
            "uint64" => "UInt64",
            "sint64" => "Int64",
            "real32" => "Single",
            "real64" => "Double",
            "boolean" => "Boolean",
            "string" => "String",
            "datetime" => "DateTime",
            "reference" => "Reference",
            "char16" => "Char",
            "object" => "Object",
            _ => wmiType
        };
    }
}