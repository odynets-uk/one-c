using OneC.Domain.ValueObjects;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="OneCRef" />.
/// </summary>
public class OneCRefTests
{
    [Fact]
    public void FromString_ValidGuid_ReturnsRef()
    {
        const string guid = "9d8fb587-9a93-11e6-80e0-1078d2d7c888";

        var result = OneCRef.FromString(guid);

        Assert.NotNull(result);
        Assert.Equal(guid, result!.Value.ToString());
        Assert.False(result.Value.IsEmpty);
    }

    [Fact]
    public void FromString_EmptyGuid_ReturnsNull()
    {
        const string emptyGuid = "00000000-0000-0000-0000-000000000000";

        var result = OneCRef.FromString(emptyGuid);

        Assert.Null(result);
    }

    [Fact]
    public void FromString_Null_ReturnsNull()
    {
        var result = OneCRef.FromString(null);

        Assert.Null(result);
    }

    [Fact]
    public void FromString_EmptyString_ReturnsNull()
    {
        var result = OneCRef.FromString(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void FromString_Whitespace_ReturnsNull()
    {
        var result = OneCRef.FromString("   ");

        Assert.Null(result);
    }

    [Fact]
    public void FromString_InvalidValue_Throws()
    {
        // e.g. if a Description accidentally lands in a Ref field.
        Assert.Throws<InvalidOperationException>(() => OneCRef.FromString("Бухгалтерські бланки"));
    }

    [Fact]
    public void FromString_InvalidFormat_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => OneCRef.FromString("not-a-guid"));
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        const string guid = "9d8fb587-9a93-11e6-80e0-1078d2d7c888";

        var result = OneCRef.FromString(guid);

        Assert.Equal(guid, result!.ToString());
    }
}