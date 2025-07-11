using NUnit.Framework;
using WmiExplorer.PropertyGrid.Editors.TypeEditors;

namespace WmiExplorer.Tests;

[TestFixture]
public class PropertyEditorIntegerValidationTests
{
    #region methods

    [TestCase("256")]
    [TestCase("-1")]
    public void IntegerEditor_Rejects_Byte(string input)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (byte)0);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("2147483648")]
    [TestCase("-2147483649")]
    public void IntegerEditor_Rejects_Int(string input)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, 0);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("9223372036854775808")]
    [TestCase("-9223372036854775809")]
    public void IntegerEditor_Rejects_Long(string input)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, 0L);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("128")]
    [TestCase("-129")]
    public void IntegerEditor_Rejects_SByte(string input)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (sbyte)0);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("32768")]
    [TestCase("-32769")]
    public void IntegerEditor_Rejects_Short(string input)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (short)0);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("4294967296")]
    [TestCase("-1")]
    public void IntegerEditor_Rejects_UInt(string input)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, 0u);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("18446744073709551616")]
    [TestCase("-1")]
    public void IntegerEditor_Rejects_ULong(string input)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, 0UL);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("65536")]
    [TestCase("-1")]
    public void IntegerEditor_Rejects_UShort(string input)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (ushort)0);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    // BYTE
    [TestCase("0", (byte)0)]
    [TestCase("255", (byte)255)]
    [TestCase("0xFF", (byte)255)]
    public void IntegerEditor_Validates_Byte(string input, byte expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (byte)expected);
        Assert.That(result.IsValid, $"Input '{input}' for byte should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is byte v && v == expected, $"Parsed value for '{input}' should be byte {expected}, but was {result.ParsedValue}");
    }

    // UINT
    [TestCase("0", 0u)]
    [TestCase("4294967295", 4294967295u)]
    [TestCase("0xFFFFFFFF", 4294967295u)]
    public void IntegerEditor_Validates_UInt(string input, uint expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, expected);
        Assert.That(result.IsValid, $"Input '{input}' for uint should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is uint v && v == expected, $"Parsed value for '{input}' should be uint {expected}, but was {result.ParsedValue}");
    }

    // ULONG
    [TestCase("0", 0UL)]
    [TestCase("18446744073709551615", 18446744073709551615UL)]
    [TestCase("0xFFFFFFFFFFFFFFFF", 18446744073709551615UL)]
    public void IntegerEditor_Validates_ULong(string input, ulong expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, expected);
        Assert.That(result.IsValid, $"Input '{input}' for ulong should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is ulong v && v == expected, $"Parsed value for '{input}' should be ulong {expected}, but was {result.ParsedValue}");
    }

    // USHORT
    [TestCase("0", (ushort)0)]
    [TestCase("65535", (ushort)65535)]
    [TestCase("0xFFFF", (ushort)65535)]
    public void IntegerEditor_Validates_UShort(string input, ushort expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (ushort)expected);
        Assert.That(result.IsValid, $"Input '{input}' for ushort should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is ushort v && v == expected, $"Parsed value for '{input}' should be ushort {expected}, but was {result.ParsedValue}");
    }

    // INT
    [TestCase("-2147483648", -2147483648)]
    [TestCase("2147483647", 2147483647)]
    [TestCase("0x7FFFFFFF", 2147483647)]
    [TestCase("0x80000000", -2147483648)]
    // two's complement
    public void IntegerEditor_Validates_Int(string input, int expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, expected);
        Assert.That(result.IsValid, $"Input '{input}' for int should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is int v && v == expected, $"Parsed value for '{input}' should be int {expected}, but was {result.ParsedValue}");
    }

    // LONG
    [TestCase("-9223372036854775808", -9223372036854775808L)]
    [TestCase("9223372036854775807", 9223372036854775807L)]
    [TestCase("0x7FFFFFFFFFFFFFFF", 9223372036854775807L)]
    [TestCase("0x8000000000000000", -9223372036854775808L)]
    // two's complement
    public void IntegerEditor_Validates_Long(string input, long expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, expected);
        Assert.That(result.IsValid, $"Input '{input}' for long should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is long v && v == expected, $"Parsed value for '{input}' should be long {expected}, but was {result.ParsedValue}");
    }

    // SBYTE
    [TestCase("-128", (sbyte)-128)]
    [TestCase("127", (sbyte)127)]
    [TestCase("0x7F", (sbyte)127)]
    [TestCase("0x80", (sbyte)-128)]
    // two's complement
    public void IntegerEditor_Validates_SByte(string input, sbyte expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (sbyte)expected);
        Assert.That(result.IsValid, $"Input '{input}' for sbyte should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is sbyte v && v == expected, $"Parsed value for '{input}' should be sbyte {expected}, but was {result.ParsedValue}");
    }

    // SHORT
    [TestCase("-32768", (short)-32768)]
    [TestCase("32767", (short)32767)]
    [TestCase("0x7FFF", (short)32767)]
    [TestCase("0x8000", (short)-32768)]
    // two's complement
    public void IntegerEditor_Validates_Short(string input, short expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (short)expected);
        Assert.That(result.IsValid, $"Input '{input}' for short should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is short v && v == expected, $"Parsed value for '{input}' should be short {expected}, but was {result.ParsedValue}");
    }

    #endregion 
}