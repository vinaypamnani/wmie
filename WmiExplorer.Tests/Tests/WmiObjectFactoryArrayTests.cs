using NUnit.Framework;
using WmiExplorer.Presentation.ViewModels.Helpers;

namespace WmiExplorer.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class WmiObjectFactoryArrayTests
{
    [Test]
    public void TestConvertStringToType()
    {
        var convertMethod = typeof(WmiObjectFactory).GetMethod("ConvertStringToType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Test string to int conversion
        var intResult = convertMethod?.Invoke(null, new object[] { "42", typeof(int) });
        Assert.That(intResult, Is.EqualTo(42));

        // Test string to double conversion
        var doubleResult = convertMethod?.Invoke(null, new object[] { "3.14", typeof(double) });
        Assert.That(doubleResult, Is.EqualTo(3.14));

        // Test string to bool conversion
        var boolResult = convertMethod?.Invoke(null, new object[] { "true", typeof(bool) });
        Assert.That(boolResult, Is.EqualTo(true));

        // Test string to string conversion
        var stringResult = convertMethod?.Invoke(null, new object[] { "hello", typeof(string) });
        Assert.That(stringResult, Is.EqualTo("hello"));
    }

    [Test]
    public void TestGetArrayElementTypeFromCimType()
    {
        var getTypeMethod = typeof(WmiObjectFactory).GetMethod("GetArrayElementTypeFromCimType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Test various CIM type mappings
        var stringType = getTypeMethod?.Invoke(null, new object[] { "string[]" });
        Assert.That(stringType, Is.EqualTo(typeof(string)));

        var intType = getTypeMethod?.Invoke(null, new object[] { "sint32[]" });
        Assert.That(intType, Is.EqualTo(typeof(int)));

        var doubleType = getTypeMethod?.Invoke(null, new object[] { "real64[]" });
        Assert.That(doubleType, Is.EqualTo(typeof(double)));

        var boolType = getTypeMethod?.Invoke(null, new object[] { "boolean[]" });
        Assert.That(boolType, Is.EqualTo(typeof(bool)));
    }

    [Test]
    public void TestIntegerArrayConversion()
    {
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Test integer array conversion
        var result = (Array?)parseMethod?.Invoke(null, new object[] { "1; 2; 3; 4", "sint32[]" });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(4));
        Assert.That(result.GetValue(0), Is.EqualTo(1));
        Assert.That(result.GetValue(1), Is.EqualTo(2));
        Assert.That(result.GetValue(2), Is.EqualTo(3));
        Assert.That(result.GetValue(3), Is.EqualTo(4));
    }

    /// <summary>
    /// Test array conversion from string values in WmiObjectFactory
    /// </summary>
    [Test]
    public void TestStringToArrayConversion()
    {
        // Use reflection to test the private ParseStringToArray method
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Test string array conversion
        var result = (Array?)parseMethod?.Invoke(null, new object[] { "hello, world, test", "string[]" });

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(result.GetValue(0), Is.EqualTo("hello"));
        Assert.That(result.GetValue(1), Is.EqualTo("world"));
        Assert.That(result.GetValue(2), Is.EqualTo("test"));
    }

    [Test]
    public void TestCimTypeArrayMappingAndParsing_UInt32()
    {
        var getTypeMethod = typeof(WmiObjectFactory).GetMethod("GetArrayElementTypeFromCimType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var uint32Type = getTypeMethod?.Invoke(null, new object[] { "uint32[]" });
        Assert.That(uint32Type, Is.EqualTo(typeof(int))); // maps to int
        var uint32Arr = (Array?)parseMethod?.Invoke(null, new object[] { "1, 2, 3, 4", "uint32[]" });
        Assert.That(uint32Arr, Is.Not.Null);
        Assert.That(uint32Arr.Length, Is.EqualTo(4));
        Assert.That(uint32Arr.GetValue(0), Is.EqualTo(1));
        Assert.That(uint32Arr.GetValue(3), Is.EqualTo(4));
    }

    [Test]
    public void TestCimTypeArrayMappingAndParsing_SInt64()
    {
        var getTypeMethod = typeof(WmiObjectFactory).GetMethod("GetArrayElementTypeFromCimType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var sint64Type = getTypeMethod?.Invoke(null, new object[] { "sint64[]" });
        Assert.That(sint64Type, Is.EqualTo(typeof(long)));
        var sint64Arr = (Array?)parseMethod?.Invoke(null, new object[] { "-1, 0, 9223372036854775807", "sint64[]" });
        Assert.That(sint64Arr, Is.Not.Null);
        Assert.That(sint64Arr.Length, Is.EqualTo(3));
        Assert.That(sint64Arr.GetValue(0), Is.EqualTo(-1L));
        Assert.That(sint64Arr.GetValue(2), Is.EqualTo(9223372036854775807L));
    }

    [Test]
    public void TestCimTypeArrayMappingAndParsing_UInt64()
    {
        var getTypeMethod = typeof(WmiObjectFactory).GetMethod("GetArrayElementTypeFromCimType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var uint64Type = getTypeMethod?.Invoke(null, new object[] { "uint64[]" });
        Assert.That(uint64Type, Is.EqualTo(typeof(long))); // maps to long
        var uint64Arr = (Array?)parseMethod?.Invoke(null, new object[] { "0, 9223372036854775807", "uint64[]" });
        Assert.That(uint64Arr, Is.Not.Null);
        Assert.That(uint64Arr.Length, Is.EqualTo(2));
        Assert.That(uint64Arr.GetValue(0), Is.EqualTo(0L));
        Assert.That(uint64Arr.GetValue(1), Is.EqualTo(9223372036854775807L));
    }

    [Test]
    public void TestCimTypeArrayMappingAndParsing_SInt16()
    {
        var getTypeMethod = typeof(WmiObjectFactory).GetMethod("GetArrayElementTypeFromCimType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var sint16Type = getTypeMethod?.Invoke(null, new object[] { "sint16[]" });
        Assert.That(sint16Type, Is.EqualTo(typeof(short)));
        var sint16Arr = (Array?)parseMethod?.Invoke(null, new object[] { "-32768, 0, 32767", "sint16[]" });
        Assert.That(sint16Arr, Is.Not.Null);
        Assert.That(sint16Arr.Length, Is.EqualTo(3));
        Assert.That(sint16Arr.GetValue(0), Is.EqualTo((short)-32768));
        Assert.That(sint16Arr.GetValue(2), Is.EqualTo((short)32767));
    }

    [Test]
    public void TestCimTypeArrayMappingAndParsing_UInt16()
    {
        var getTypeMethod = typeof(WmiObjectFactory).GetMethod("GetArrayElementTypeFromCimType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var uint16Type = getTypeMethod?.Invoke(null, new object[] { "uint16[]" });
        Assert.That(uint16Type, Is.EqualTo(typeof(ushort))); // maps to ushort
        var uint16Arr = (Array?)parseMethod?.Invoke(null, new object[] { "0, 65535", "uint16[]" });
        Assert.That(uint16Arr, Is.Not.Null);
        Assert.That(uint16Arr.Length, Is.EqualTo(2));
        Assert.That(uint16Arr.GetValue(0), Is.EqualTo((ushort)0));
        Assert.That(uint16Arr.GetValue(1), Is.EqualTo((ushort)65535));
    }

    [Test]
    public void TestCimTypeArrayMappingAndParsing_SInt8()
    {
        var getTypeMethod = typeof(WmiObjectFactory).GetMethod("GetArrayElementTypeFromCimType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var sint8Type = getTypeMethod?.Invoke(null, new object[] { "sint8[]" });
        Assert.That(sint8Type, Is.EqualTo(typeof(byte)));
        var sint8Arr = (Array?)parseMethod?.Invoke(null, new object[] { "0, 127", "sint8[]" });
        Assert.That(sint8Arr, Is.Not.Null);
        Assert.That(sint8Arr.Length, Is.EqualTo(2));
        Assert.That(sint8Arr.GetValue(0), Is.EqualTo((byte)0));
        Assert.That(sint8Arr.GetValue(1), Is.EqualTo((byte)127));
    }

    [Test]
    public void TestCimTypeArrayMappingAndParsing_UInt8()
    {
        var getTypeMethod = typeof(WmiObjectFactory).GetMethod("GetArrayElementTypeFromCimType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var parseMethod = typeof(WmiObjectFactory).GetMethod("ParseStringToArray",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var uint8Type = getTypeMethod?.Invoke(null, new object[] { "uint8[]" });
        Assert.That(uint8Type, Is.EqualTo(typeof(byte)));
        var uint8Arr = (Array?)parseMethod?.Invoke(null, new object[] { "0, 255", "uint8[]" });
        Assert.That(uint8Arr, Is.Not.Null);
        Assert.That(uint8Arr.Length, Is.EqualTo(2));
        Assert.That(uint8Arr.GetValue(0), Is.EqualTo((byte)0));
        Assert.That(uint8Arr.GetValue(1), Is.EqualTo((byte)255));
    }
}