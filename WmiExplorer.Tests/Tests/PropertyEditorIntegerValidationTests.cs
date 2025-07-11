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
    [TestCase("0", byte.MinValue)]
    [TestCase("255", byte.MaxValue)]
    [TestCase("0xFF", byte.MaxValue)]
    public void IntegerEditor_Validates_Byte(string input, byte expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (byte)expected);
        Assert.That(result.IsValid, $"Input '{input}' for byte should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is byte v && v == expected, $"Parsed value for '{input}' should be byte {expected}, but was {result.ParsedValue}");
    }

    // UINT
    [TestCase("0", uint.MinValue)]
    [TestCase("4294967295", uint.MaxValue)]
    [TestCase("0xFFFFFFFF", uint.MaxValue)]
    public void IntegerEditor_Validates_UInt(string input, uint expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, expected);
        Assert.That(result.IsValid, $"Input '{input}' for uint should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is uint v && v == expected, $"Parsed value for '{input}' should be uint {expected}, but was {result.ParsedValue}");
    }

    // ULONG
    [TestCase("0", ulong.MinValue)]
    [TestCase("18446744073709551615", ulong.MaxValue)]
    [TestCase("0xFFFFFFFFFFFFFFFF", ulong.MaxValue)]
    public void IntegerEditor_Validates_ULong(string input, ulong expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, expected);
        Assert.That(result.IsValid, $"Input '{input}' for ulong should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is ulong v && v == expected, $"Parsed value for '{input}' should be ulong {expected}, but was {result.ParsedValue}");
    }

    // USHORT
    [TestCase("0", ushort.MinValue)]
    [TestCase("65535", ushort.MaxValue)]
    [TestCase("0xFFFF", ushort.MaxValue)]
    public void IntegerEditor_Validates_UShort(string input, ushort expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (ushort)expected);
        Assert.That(result.IsValid, $"Input '{input}' for ushort should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is ushort v && v == expected, $"Parsed value for '{input}' should be ushort {expected}, but was {result.ParsedValue}");
    }

    // INT
    [TestCase("-2147483648", int.MinValue)]
    [TestCase("2147483647", int.MaxValue)]
    [TestCase("0x7FFFFFFF", int.MaxValue)]
    [TestCase("0x80000000", int.MinValue)]
    // two's complement
    public void IntegerEditor_Validates_Int(string input, int expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, expected);
        Assert.That(result.IsValid, $"Input '{input}' for int should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is int v && v == expected, $"Parsed value for '{input}' should be int {expected}, but was {result.ParsedValue}");
    }

    // LONG
    [TestCase("-9223372036854775808", long.MinValue)]
    [TestCase("9223372036854775807", long.MaxValue)]
    [TestCase("0x7FFFFFFFFFFFFFFF", long.MaxValue)]
    [TestCase("0x8000000000000000", long.MinValue)]
    // two's complement
    public void IntegerEditor_Validates_Long(string input, long expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, expected);
        Assert.That(result.IsValid, $"Input '{input}' for long should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is long v && v == expected, $"Parsed value for '{input}' should be long {expected}, but was {result.ParsedValue}");
    }

    // SBYTE
    [TestCase("-128", sbyte.MinValue)]
    [TestCase("127", sbyte.MaxValue)]
    [TestCase("0x7F", sbyte.MaxValue)]
    [TestCase("0x80", sbyte.MinValue)]
    // two's complement
    public void IntegerEditor_Validates_SByte(string input, sbyte expected)
    {
        var result = IntegerEditor.CustomIntegerValidation(input, (sbyte)expected);
        Assert.That(result.IsValid, $"Input '{input}' for sbyte should be valid");
        Assert.That(result.ParsedValue, Is.Not.Null, $"Parsed value for '{input}' should not be null");
        Assert.That(result.ParsedValue is sbyte v && v == expected, $"Parsed value for '{input}' should be sbyte {expected}, but was {result.ParsedValue}");
    }

    // SHORT
    [TestCase("-32768", short.MinValue)]
    [TestCase("32767", short.MaxValue)]
    [TestCase("0x7FFF", short.MaxValue)]
    [TestCase("0x8000", short.MinValue)]
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