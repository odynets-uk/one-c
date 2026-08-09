using System.Text.Encodings.Web;
using System.Text.Json;
using OneC.Infrastructure.Json;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="DBNullJsonConverter" />.
/// </summary>
public class DBNullJsonConverterTests
{
    [Fact]
    public void Serialize_DBNullValue_WritesJsonNull()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new DBNullJsonConverter() },
        };

        var json = JsonSerializer.Serialize(DBNull.Value, options);

        Assert.Equal("null", json);
    }

    [Fact]
    public void Serialize_DictionaryWithDBNull_WritesNullNotObject()
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new DBNullJsonConverter() },
        };

        var dict = new Dictionary<string, object?>
        {
            ["comment"] = DBNull.Value,
            ["name"] = "Недатовані",
        };

        var json = JsonSerializer.Serialize(dict, options);

        Assert.Contains("\"comment\":null", json);
        Assert.Contains("\"name\":\"Недатовані\"", json);
        Assert.DoesNotContain("\"comment\":{}", json);
    }
}