using NUnit.Framework;
using WmiExplorer.Presentation.ViewModels.Helpers;

namespace WmiExplorer.TestAvalonEdit.Tests;

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
}