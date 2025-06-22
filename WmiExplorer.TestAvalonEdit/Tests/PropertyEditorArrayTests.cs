using NUnit.Framework;
using WmiExplorer.PropertyGrid;

namespace WmiExplorer.TestAvalonEdit.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class PropertyEditorArrayTests
{
    [Test, STAThread]
    public void TestArrayFormatting()
    {
        var editor = new PropertyEditor();

        var formatMethod = typeof(PropertyEditor).GetMethod("FormatArrayValueForEditing",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Test formatting an integer array
        var intArray = new int[] { 1, 2, 3, 4 };
        var result = (string?)formatMethod?.Invoke(editor, new object[] { intArray });

        Assert.That(result, Is.EqualTo("1, 2, 3, 4"));

        // Test formatting a string array
        var stringArray = new string[] { "hello", "world", "test" };
        result = (string?)formatMethod?.Invoke(editor, new object[] { stringArray });

        Assert.That(result, Is.EqualTo("hello, world, test"));
    }

    [Test, STAThread]
    public void TestArrayParsing_EmptyInput()
    {
        var editor = new PropertyEditor();

        var parseMethod = typeof(PropertyEditor).GetMethod("ParseArrayValueFromText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Test empty input
        var result = (Array?)parseMethod?.Invoke(editor, new object[] { "", typeof(string) });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(0));
    }

    /// Test that array parsing works correctly for various data types and separators
    /// </summary>
    [Test, STAThread]
    public void TestArrayParsing_IntegerArray()
    {
        // Create a mock PropertyHierarchyItem for testing
        var propertyItem = CreateMockPropertyItem(typeof(int[]), null);
        var editor = new PropertyEditor();

        // Use reflection to access the private method
        var parseMethod = typeof(PropertyEditor).GetMethod("ParseArrayValueFromText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Test comma-separated integers
        var result = (Array?)parseMethod?.Invoke(editor, new object[] { "1, 2, 3, 4", typeof(int) });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(4));
        Assert.That(result.GetValue(0), Is.EqualTo(1));
        Assert.That(result.GetValue(1), Is.EqualTo(2));
        Assert.That(result.GetValue(2), Is.EqualTo(3));
        Assert.That(result.GetValue(3), Is.EqualTo(4));
    }

    [Test, STAThread]
    public void TestArrayParsing_MixedSeparators()
    {
        var propertyItem = CreateMockPropertyItem(typeof(string[]), null);
        var editor = new PropertyEditor();

        var parseMethod = typeof(PropertyEditor).GetMethod("ParseArrayValueFromText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Test mixed comma and semicolon separators
        var result = (Array?)parseMethod?.Invoke(editor, new object[] { "first, second; third, fourth", typeof(string) });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(4));
        Assert.That(result.GetValue(0), Is.EqualTo("first"));
        Assert.That(result.GetValue(1), Is.EqualTo("second"));
        Assert.That(result.GetValue(2), Is.EqualTo("third"));
        Assert.That(result.GetValue(3), Is.EqualTo("fourth"));
    }

    [Test, STAThread]
    public void TestArrayParsing_StringArray()
    {
        var propertyItem = CreateMockPropertyItem(typeof(string[]), null);
        var editor = new PropertyEditor();

        var parseMethod = typeof(PropertyEditor).GetMethod("ParseArrayValueFromText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Test semicolon-separated strings
        var result = (Array?)parseMethod?.Invoke(editor, new object[] { "hello; world; test", typeof(string) });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("hello"));
        Assert.That(result.GetValue(1), Is.EqualTo("world"));
        Assert.That(result.GetValue(2), Is.EqualTo("test"));
    }

    [Test, STAThread]
    public void TestArrayParsing_WhitespaceHandling()
    {
        var editor = new PropertyEditor();

        var parseMethod = typeof(PropertyEditor).GetMethod("ParseArrayValueFromText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Test input with extra whitespace
        var result = (Array?)parseMethod?.Invoke(editor, new object[] { "  first  ,  second  ;  third  ", typeof(string) });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("first"));
        Assert.That(result.GetValue(1), Is.EqualTo("second"));
        Assert.That(result.GetValue(2), Is.EqualTo("third"));
    }

    /// <summary>
    /// Creates a mock PropertyHierarchyItem for testing purposes
    /// </summary>
    private PropertyHierarchyItem CreateMockPropertyItem(Type propertyType, object? value)
    {
        // Since PropertyHierarchyItem might be complex to mock, we'll create a simple test version
        // For a complete test, we'd need to properly mock this class
        return new PropertyHierarchyItem
        {
            PropertyType = propertyType,
            Value = value,
            IsReadOnly = false
        };
    }
}