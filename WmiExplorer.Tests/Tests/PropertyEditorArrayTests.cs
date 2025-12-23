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
    public void TestArrayParsing_ByteArray()
    {
        var result = ArrayEditor.ParseArrayValueFromText("0, 1, 255", typeof(byte));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo((byte)0));
        Assert.That(result.GetValue(1), Is.EqualTo((byte)1));
        Assert.That(result.GetValue(2), Is.EqualTo((byte)255));
        var formatted = ArrayEditor.FormatArrayValueForEditing(result);
        Assert.That(formatted, Is.EqualTo("0, 1, 255"));
    }

    [Test, STAThread]
    public void TestArrayParsing_EmptyInput()
    {
        // Test empty input
        var result = ArrayEditor.ParseArrayValueFromText("", typeof(string));

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(0));
    }

    [Test, STAThread]
    public void TestArrayParsing_EmptyQuotedValues()
    {
        // "",foo,"" => ["", foo, ""]
        var result = ArrayEditor.ParseArrayValueFromText("\"\",foo,\"\"", typeof(string));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo(""));
        Assert.That(result.GetValue(1), Is.EqualTo("foo"));
        Assert.That(result.GetValue(2), Is.EqualTo(""));
    }

    [Test, STAThread]
    public void TestArrayParsing_EscapedQuotes()
    {
        // "foo""bar",baz => [foo"bar, baz]
        var result = ArrayEditor.ParseArrayValueFromText("\"foo\"\"bar\",baz", typeof(string));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(2));
        Assert.That(result.GetValue(0), Is.EqualTo("foo\"bar"));
        Assert.That(result.GetValue(1), Is.EqualTo("baz"));
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
    public void TestArrayParsing_LongArray()
    {
        var result = ArrayEditor.ParseArrayValueFromText("10000000000, 20000000000", typeof(long));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(2));
        Assert.That(result.GetValue(0), Is.EqualTo(10000000000L));
        Assert.That(result.GetValue(1), Is.EqualTo(20000000000L));
        var formatted = ArrayEditor.FormatArrayValueForEditing(result);
        Assert.That(formatted, Is.EqualTo("10000000000, 20000000000"));
    }

    [Test, STAThread]
    public void TestArrayParsing_MixedQuotes_IntegerArray()
    {
        // Test mixed quoted and unquoted integers
        var result = ArrayEditor.ParseArrayValueFromText("1, \"2\", 3", typeof(int));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo(1));
        Assert.That(result.GetValue(1), Is.EqualTo(2));
        Assert.That(result.GetValue(2), Is.EqualTo(3));
    }

    [Test, STAThread]
    public void TestArrayParsing_MixedQuotes_StringArray()
    {
        // Test mixed quoted and unquoted strings
        var result = ArrayEditor.ParseArrayValueFromText("One, Two, \"Three\"", typeof(string));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("One"));
        Assert.That(result.GetValue(1), Is.EqualTo("Two"));
        Assert.That(result.GetValue(2), Is.EqualTo("Three"));
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
    public void TestArrayParsing_QuotedDelimiters()
    {
        // "a,b;c",d => [a,b;c, d]
        var result = ArrayEditor.ParseArrayValueFromText("\"a,b;c\",d,\"e,f\"", typeof(string));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("a,b;c"));
        Assert.That(result.GetValue(1), Is.EqualTo("d"));
        Assert.That(result.GetValue(2), Is.EqualTo("e,f"));
    }

    [Test, STAThread]
    public void TestArrayParsing_QuotedElementsWithDelimiters()
    {
        // Test quoted elements containing commas and semicolons
        var result = ArrayEditor.ParseArrayValueFromText("1, \"2,\", \"3;\", \"four\"", typeof(string));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(4));
        Assert.That(result.GetValue(0), Is.EqualTo("1"));
        Assert.That(result.GetValue(1), Is.EqualTo("2,"));
        Assert.That(result.GetValue(2), Is.EqualTo("3;"));
        Assert.That(result.GetValue(3), Is.EqualTo("four"));
    }

    [Test, STAThread]
    public void TestArrayParsing_QuotedIntegerArray()
    {
        // Quotes should be stripped and values parsed as numbers
        var result = ArrayEditor.ParseArrayValueFromText("\"-1\", \"0\", \"2\"", typeof(int));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo(-1));
        Assert.That(result.GetValue(1), Is.EqualTo(0));
        Assert.That(result.GetValue(2), Is.EqualTo(2));
    }

    [Test, STAThread]
    public void TestArrayParsing_QuotedStringArray()
    {
        // Quotes should be preserved as part of the string value
        var result = ArrayEditor.ParseArrayValueFromText("\"foo\", \"bar\", \"baz\"", typeof(string));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("foo"));
        Assert.That(result.GetValue(1), Is.EqualTo("bar"));
        Assert.That(result.GetValue(2), Is.EqualTo("baz"));
    }

    [Test, STAThread]
    public void TestArrayParsing_SByteArray()
    {
        var result = ArrayEditor.ParseArrayValueFromText("-128, 0, 127", typeof(sbyte));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo((sbyte)-128));
        Assert.That(result.GetValue(1), Is.EqualTo((sbyte)0));
        Assert.That(result.GetValue(2), Is.EqualTo((sbyte)127));
        var formatted = ArrayEditor.FormatArrayValueForEditing(result);
        Assert.That(formatted, Is.EqualTo("-128, 0, 127"));
    }

    [Test, STAThread]
    public void TestArrayParsing_ShortArray()
    {
        var result = ArrayEditor.ParseArrayValueFromText("-1, 0, 1, 32767", typeof(short));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(4));
        Assert.That(result.GetValue(0), Is.EqualTo((short)-1));
        Assert.That(result.GetValue(1), Is.EqualTo((short)0));
        Assert.That(result.GetValue(2), Is.EqualTo((short)1));
        Assert.That(result.GetValue(3), Is.EqualTo((short)32767));
        var formatted = ArrayEditor.FormatArrayValueForEditing(result);
        Assert.That(formatted, Is.EqualTo("-1, 0, 1, 32767"));
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
    public void TestArrayParsing_UIntArray()
    {
        var result = ArrayEditor.ParseArrayValueFromText("1, 2, 3, 4", typeof(uint));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(4));
        Assert.That(result.GetValue(0), Is.EqualTo(1u));
        Assert.That(result.GetValue(1), Is.EqualTo(2u));
        Assert.That(result.GetValue(2), Is.EqualTo(3u));
        Assert.That(result.GetValue(3), Is.EqualTo(4u));
        var formatted = ArrayEditor.FormatArrayValueForEditing(result);
        Assert.That(formatted, Is.EqualTo("1, 2, 3, 4"));
    }

    [Test, STAThread]
    public void TestArrayParsing_ULongArray()
    {
        var result = ArrayEditor.ParseArrayValueFromText("10000000000, 20000000000", typeof(ulong));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(2));
        Assert.That(result.GetValue(0), Is.EqualTo(10000000000UL));
        Assert.That(result.GetValue(1), Is.EqualTo(20000000000UL));
        var formatted = ArrayEditor.FormatArrayValueForEditing(result);
        Assert.That(formatted, Is.EqualTo("10000000000, 20000000000"));
    }

    [Test, STAThread]
    public void TestArrayParsing_UShortArray()
    {
        var result = ArrayEditor.ParseArrayValueFromText("0, 1, 65535", typeof(ushort));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo((ushort)0));
        Assert.That(result.GetValue(1), Is.EqualTo((ushort)1));
        Assert.That(result.GetValue(2), Is.EqualTo((ushort)65535));
        var formatted = ArrayEditor.FormatArrayValueForEditing(result);
        Assert.That(formatted, Is.EqualTo("0, 1, 65535"));
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

    [Test, STAThread]
    public void TestArrayParsing_WhitespaceInsideAndOutsideQuotes()
    {
        // "  foo  ", bar ,"baz " => [  foo  , bar, baz ]
        var result = ArrayEditor.ParseArrayValueFromText("\"  foo  \", bar ,\"baz \"", typeof(string));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("  foo  "));
        Assert.That(result.GetValue(1), Is.EqualTo("bar"));
        Assert.That(result.GetValue(2), Is.EqualTo("baz "));
    }
}