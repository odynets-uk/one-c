using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OneC.Domain.Profiles;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Maps COM values from 1C to .NET types and applies transformations and validation.
/// </summary>
public sealed class ComValueMapper
{
    private readonly IComSession _session;
    private readonly ILogger<ComValueMapper> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComValueMapper" /> class.
    /// </summary>
    /// <param name="session">An established COM session (used to normalize 1C empty values).</param>
    /// <param name="logger">Logger instance.</param>
    public ComValueMapper(IComSession session, ILogger<ComValueMapper> logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <summary>
    ///     Maps a raw COM value to a target value according to the column definition.
    ///     Applies transformation (if any) and validation rules.
    /// </summary>
    /// <param name="rawValue">Raw COM value.</param>
    /// <param name="column">Column definition with source mapping.</param>
    /// <returns>Mapped value, or null if the value is null/empty.</returns>
    /// <exception cref="InvalidOperationException">When validation fails.</exception>
    public object? Map(object? rawValue, ProfileColumn column)
    {
        // First: normalize 1C empty values (Undefined/NULL COM wrappers) to real null.
        var normalized = Normalize(rawValue);

        // Then: transform the raw value.
        var transformed = ApplyTransform(normalized, column.Transform);

        // Normalize empty JSON elements (e.g. null/everything from 1C) to null.
        if (transformed is System.Text.Json.JsonElement je)
        {
            transformed = je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Null => null,
                System.Text.Json.JsonValueKind.String when string.IsNullOrEmpty(je.GetString()) => null,
                System.Text.Json.JsonValueKind.Object when je.EnumerateObject().Any() == false => null,
                _ => transformed,
            };
        }

        // Then: validate.
        Validate(transformed, column);

        return transformed;
    }

    /// <summary>
    ///     Converts 1C "Undefined" and database "NULL" COM values to real .NET null.
    ///     The COM connector returns RCW wrappers (System.__ComObject) for 1C empty types,
    ///     which would otherwise serialize as "{}" instead of null.
    /// </summary>
    private object? Normalize(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        if (Marshal.IsComObject(value))
        {
            try
            {
                // Built-in 1C function: Строка(Неопределено) -> "", Строка(NULL) -> "Null".
                var asString = _session.String(value);
                if (string.IsNullOrEmpty(asString))
                {
                    return null; // Undefined / empty value.
                }

                // Also check the exact type name via ТипЗнч.
                var typeName = _session.String(_session.Connection.ТипЗнч(value));
                if (typeName is "Неопределено" or "Null")
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to normalize COM value: {Message}", ex.Message);
                return null;
            }
        }

        return value;
    }

    private static object? ApplyTransform(object? value, string? transform)
    {
        if (string.IsNullOrWhiteSpace(transform) || value is null)
        {
            return value;
        }

        // Support "NOT {value}" for boolean inversion.
        if (transform.Contains("{value}", StringComparison.Ordinal))
        {
            var expression = transform.Replace("{value}", value.ToString(), StringComparison.Ordinal);
            if (transform.Trim().StartsWith("NOT ", StringComparison.OrdinalIgnoreCase))
            {
                return value is bool b ? !b : value;
            }
        }

        return value;
    }

    private void Validate(object? value, ProfileColumn column)
    {
        var validation = column.Validation;
        if (validation is null)
        {
            return;
        }

        var valueStr = value?.ToString();

        // Required: value must not be null/empty.
        if (validation.Required && string.IsNullOrWhiteSpace(valueStr))
        {
            throw new InvalidOperationException(
                $"Column '{column.Name}': value is required but was null/empty.");
        }

        // Nullable: if not null, continue; if null and not nullable and not required, it's allowed
        // (only matters when the value is null and Required is false).
        if (value is null && !validation.Nullable && !validation.Required)
        {
            // Empty value in 1C often means "empty reference" — allowed by default.
            return;
        }

        // Regex.
        if (!string.IsNullOrWhiteSpace(validation.Regex) && !string.IsNullOrWhiteSpace(valueStr))
        {
            var options = RegexOptions.None;
            if (string.Equals(validation.Case, "insensitive", StringComparison.OrdinalIgnoreCase))
            {
                options = RegexOptions.IgnoreCase;
            }

            var regex = new Regex(validation.Regex, options);
            if (!regex.IsMatch(valueStr))
            {
                throw new InvalidOperationException(
                    $"Column '{column.Name}': value '{valueStr}' does not match regex '{validation.Regex}'.");
            }
        }

        // MinLength / MaxLength.
        if (!string.IsNullOrWhiteSpace(valueStr))
        {
            if (validation.MinLength.HasValue && valueStr.Length < validation.MinLength.Value)
            {
                throw new InvalidOperationException(
                    $"Column '{column.Name}': value '{valueStr}' is shorter than min length {validation.MinLength}.");
            }

            if (validation.MaxLength.HasValue && valueStr.Length > validation.MaxLength.Value)
            {
                throw new InvalidOperationException(
                    $"Column '{column.Name}': value '{valueStr}' is longer than max length {validation.MaxLength}.");
            }
        }

        // Boolean strict.
        if (string.Equals(validation.Boolean, "strict", StringComparison.OrdinalIgnoreCase) && value is not bool)
        {
            throw new InvalidOperationException(
                $"Column '{column.Name}': value '{valueStr}' is not a strict boolean.");
        }
    }
}