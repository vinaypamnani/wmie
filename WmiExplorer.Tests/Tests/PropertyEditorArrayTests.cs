using NUnit.Framework;
using WmiExplorer.PropertyGrid.Editors.TypeEditors;

namespace WmiExplorer.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class PropertyEditorArrayTests
{
    [Test, STAThread]
    public void TestArrayFormatting()
    {
        // Test formatting an integer array
        var intArray = new int[] { 1, 2, 3, 4 };
        var result = ArrayEditor.FormatArrayValueForEditing(intArray);

        Assert.That(result, Is.EqualTo("1, 2, 3, 4"));

        // Test formatting a string array
        var stringArray = new string[] { "hello", "world", "test" };
        result = ArrayEditor.FormatArrayValueForEditing(stringArray);

        Assert.That(result, Is.EqualTo("hello, world, test"));
    }

    [Test, STAThread]
    public void TestArrayParsing_EmptyInput()
    {
        // Test empty input
        var result = ArrayEditor.ParseArrayValueFromText("", typeof(string));

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(0));
    }

    /// Test that array parsing works correctly for various data types and separators
    /// </summary>
    [Test, STAThread]
    public void TestArrayParsing_IntegerArray()
    {
        // Test comma-separated integers
        var result = ArrayEditor.ParseArrayValueFromText("1, 2, 3, 4", typeof(int));

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
        // Test mixed comma and semicolon separators
        var result = ArrayEditor.ParseArrayValueFromText("first, second; third, fourth", typeof(string));

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
        // Test semicolon-separated strings
        var result = ArrayEditor.ParseArrayValueFromText("hello; world; test", typeof(string));

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("hello"));
        Assert.That(result.GetValue(1), Is.EqualTo("world"));
        Assert.That(result.GetValue(2), Is.EqualTo("test"));
    }

    [Test, STAThread]
    public void TestArrayParsing_WhitespaceHandling()
    {
        // Test input with extra whitespace
        var result = ArrayEditor.ParseArrayValueFromText("  first  ,  second  ;  third  ", typeof(string));

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("first"));
        Assert.That(result.GetValue(1), Is.EqualTo("second"));
        Assert.That(result.GetValue(2), Is.EqualTo("third"));
    }
}