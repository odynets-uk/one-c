using System.Text.Json;
using OneC.Domain.Profiles;
using OneC.Infrastructure.Profiles;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="ProfileLoader" />.
/// </summary>
public class ProfileLoaderTests
{
    [Fact]
    public void Parse_ValidProfile_LoadsAllSections()
    {
        const string json = """
                            {
                              "name": "categories",
                              "source": { "type": "CatalogObject.Номенклатура", "schema": "./data-enterprise.xsd" },
                              "mode": "full",
                              "batch_size": -1,
                              "filters": { "field_filters": { "IsFolder": true } },
                              "output": {
                                "json": { "file_path": "export/categories.json", "pretty": true },
                                "db": { "file_path": "export/kplus.db", "engine": "sqlite", "version": "3.37+" }
                              },
                              "table": "categories",
                              "columns": [
                                { "source": "Ref", "name": "id", "sql_type": "TEXT PRIMARY KEY",
                                  "validation": { "required": true, "regex": "^[a-f0-9-]{36}$", "case": "insensitive" } },
                                { "source": "Parent", "name": "parent_id", "sql_type": "TEXT",
                                  "validation": { "nullable": true, "regex": "^[a-f0-9-]{36}$" } },
                                { "source": "Code", "name": "legacy_id", "sql_type": "TEXT NOT NULL",
                                  "validation": { "required": true, "unique": true, "regex": "^\\d{9}$" } },
                                { "source": "Description", "name": "name", "sql_type": "TEXT",
                                  "validation": { "nullable": true, "min_length": 1 } },
                                { "source": "Комментарий", "name": "comment", "sql_type": "TEXT",
                                  "validation": { "nullable": true, "min_length": 1 } },
                                { "source": "DeletionMark", "name": "is_active", "sql_type": "INTEGER NOT NULL DEFAULT 1",
                                  "transform": "NOT {value}", "validation": { "boolean": "strict" } }
                              ],
                              "references": [
                                { "column": "parent_id", "references": "categories(id)", "on_delete": "RESTRICT", "on_update": "NO ACTION" }
                              ],
                              "indexes": [
                                { "name": "categories_parent_id_index", "columns": ["parent_id"] }
                              ]
                            }
                            """;

        var profile = ProfileLoader.Parse(json, "categories");

        Assert.Equal("categories", profile.Name);
        Assert.Equal("CatalogObject.Номенклатура", profile.Source.Type);
        Assert.Equal("full", profile.Mode);
        Assert.Equal(-1, profile.BatchSize);
        Assert.NotNull(profile.Output);
        Assert.Equal("export/categories.json", profile.Output!.Json!.FilePath);
        Assert.True(profile.Output.Json.Pretty);
        Assert.Equal("export/kplus.db", profile.Output.Db!.FilePath);
        Assert.Equal("sqlite", profile.Output.Db.Engine);
        Assert.Equal("categories", profile.Table);
        Assert.Equal(6, profile.Columns.Count);
        Assert.Equal("NOT {value}", profile.Columns[5].Transform);
        Assert.Equal(1, profile.References.Count);
        Assert.Equal(1, profile.Indexes.Count);
        Assert.True(profile.Filters!.FieldFilters.ContainsKey("IsFolder"));
    }

    [Fact]
    public void Parse_WithoutName_UsesFileName()
    {
        const string json = """
                            {
                              "source": { "type": "CatalogObject.Номенклатура" },
                              "table": "categories",
                              "columns": [
                                { "source": "Ref", "name": "id", "sql_type": "TEXT PRIMARY KEY" }
                              ]
                            }
                            """;

        var profile = ProfileLoader.Parse(json, "categories");

        Assert.Equal("categories", profile.Name);
    }

    [Fact]
    public void Parse_WithoutSourceType_Throws()
    {
        const string json = """
                            {
                              "name": "test",
                              "table": "categories",
                              "columns": [
                                { "source": "Ref", "name": "id", "sql_type": "TEXT PRIMARY KEY" }
                              ]
                            }
                            """;

        Assert.Throws<JsonException>(() => ProfileLoader.Parse(json, "test"));
    }

    [Fact]
    public void Parse_WithoutTable_Throws()
    {
        const string json = """
                            {
                              "name": "test",
                              "source": { "type": "CatalogObject.Номенклатура" },
                              "columns": [
                                { "source": "Ref", "name": "id", "sql_type": "TEXT PRIMARY KEY" }
                              ]
                            }
                            """;

        Assert.Throws<JsonException>(() => ProfileLoader.Parse(json, "test"));
    }

    [Fact]
    public void Parse_WithoutIdColumn_Throws()
    {
        const string json = """
                            {
                              "name": "test",
                              "source": { "type": "CatalogObject.Номенклатура" },
                              "table": "categories",
                              "columns": [
                                { "source": "Code", "name": "code", "sql_type": "TEXT" }
                              ]
                            }
                            """;

        Assert.Throws<JsonException>(() => ProfileLoader.Parse(json, "test"));
    }

    [Fact]
    public void Parse_InvalidMode_Throws()
    {
        const string json = """
                            {
                              "name": "test",
                              "source": { "type": "CatalogObject.Номенклатура" },
                              "table": "categories",
                              "mode": "invalid",
                              "columns": [
                                { "source": "Ref", "name": "id", "sql_type": "TEXT PRIMARY KEY" }
                              ]
                            }
                            """;

        Assert.Throws<JsonException>(() => ProfileLoader.Parse(json, "test"));
    }

    [Fact]
    public void Parse_EmptyJson_Throws()
    {
        Assert.Throws<JsonException>(() => ProfileLoader.Parse("{}", "test"));
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => ProfileLoader.Load("nonexistent-profile.json"));
    }
}