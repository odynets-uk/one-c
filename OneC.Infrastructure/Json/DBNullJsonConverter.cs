using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneC.Infrastructure.Json;

/// <summary>
///     Serializes <see cref="DBNull.Value" /> as JSON null.
///     System.Text.Json would otherwise write DBNull (a class with no public properties) as "{}".
/// </summary>
public sealed class DBNullJsonConverter : JsonConverter<DBNull>
{
    /// <inheritdoc />
    public override DBNull Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("DBNull deserialization is not supported.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DBNull value, JsonSerializerOptions options)
        => writer.WriteNullValue();
}