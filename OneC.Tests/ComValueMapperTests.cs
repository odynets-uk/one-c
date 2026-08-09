using Microsoft.Extensions.Logging.Abstractions;
using OneC.Domain.Profiles;
using OneC.Infrastructure.Com;
using OneC.Infrastructure.Readers;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="ComValueMapper" />.
/// </summary>
public class ComValueMapperTests
{
    private static ComValueMapper CreateMapper(IComSession? session = null)
    {
        return new ComValueMapper(session ?? new FakeComSession(), NullLogger<ComValueMapper>.Instance);
    }

    private static ProfileColumn Column(
        string source = "Field",
        string name = "field",
        string sqlType = "TEXT",
        string? transform = null,
        ColumnValidation? validation = null)
    {
        return new ProfileColumn
        {
            Source = source,
            Name = name,
            SqlType = sqlType,
            Transform = transform,
            Validation = validation,
        };
    }

    [Fact]
    public void Map_NullValue_ReturnsNull()
    {
        var mapper = CreateMapper();
        var column = Column(validation: new ColumnValidation { Nullable = true });

        var result = mapper.Map(null, column);

        Assert.Null(result);
    }

    [Fact]
    public void Map_DBNullValue_ReturnsNull()
    {
        var mapper = CreateMapper();
        var column = Column(validation: new ColumnValidation { Nullable = true });

        var result = mapper.Map(DBNull.Value, column);

        Assert.Null(result);
    }

    [Fact]
    public void Map_EmptyJsonObject_ReturnsNull()
    {
        var mapper = CreateMapper();
        var column = Column(validation: new ColumnValidation { Nullable = true });
        var emptyObject = System.Text.Json.JsonSerializer.Deserialize<object>("{}");

        var result = mapper.Map(emptyObject, column);

        Assert.Null(result);
    }

    [Fact]
    public void Map_StringValue_ReturnsString()
    {
        var mapper = CreateMapper();
        var column = Column();

        var result = mapper.Map("hello", column);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Map_TransformNotValue_InvertsBoolean()
    {
        var mapper = CreateMapper();
        var column = Column(
            source: "DeletionMark",
            name: "is_active",
            sqlType: "INTEGER NOT NULL DEFAULT 1",
            transform: "NOT {value}",
            validation: new ColumnValidation { Boolean = "strict" });

        var result = mapper.Map(true, column);

        Assert.False((bool)result!);
    }

    [Fact]
    public void Map_RequiredButNull_Throws()
    {
        var mapper = CreateMapper();
        var column = Column(validation: new ColumnValidation { Required = true });

        Assert.Throws<InvalidOperationException>(() => mapper.Map(null, column));
    }

    [Fact]
    public void Map_RegexMismatch_Throws()
    {
        var mapper = CreateMapper();
        var column = Column(validation: new ColumnValidation { Regex = "^\\d{9}$" });

        Assert.Throws<InvalidOperationException>(() => mapper.Map("abc", column));
    }

    [Fact]
    public void Map_RegexMatch_ReturnsValue()
    {
        var mapper = CreateMapper();
        var column = Column(validation: new ColumnValidation { Regex = "^\\d{9}$" });

        var result = mapper.Map("000005906", column);

        Assert.Equal("000005906", result);
    }

    [Fact]
    public void Map_MinLengthTooShort_Throws()
    {
        var mapper = CreateMapper();
        var column = Column(validation: new ColumnValidation { MinLength = 5 });

        Assert.Throws<InvalidOperationException>(() => mapper.Map("ab", column));
    }

    [Fact]
    public void Map_BooleanStrictWithNonBool_Throws()
    {
        var mapper = CreateMapper();
        var column = Column(validation: new ColumnValidation { Boolean = "strict" });

        Assert.Throws<InvalidOperationException>(() => mapper.Map("not-a-bool", column));
    }

    /// <summary>
    ///     Minimal fake session for unit tests (no real COM).
    /// </summary>
    private sealed class FakeComSession : IComSession
    {
        public dynamic Connection => new object();

        public string String(object value)
        {
            return value?.ToString() ?? string.Empty;
        }
    }
}