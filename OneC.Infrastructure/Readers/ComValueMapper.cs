using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OneC.Domain.Profiles;
using OneC.Domain.ValueObjects;
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

        // Then: apply the domain Value Object (e.g. OneCRef) if configured.
        var voValue = ApplyValueObject(normalized, column);

        // Then: transform the raw value.
        var transformed = ApplyTransform(voValue, column.Transform);

        // Normalize empty JSON elements (e.g. null/everything from 1C) to null.
        if (transformed is JsonElement je)
        {
            transformed = je.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String when string.IsNullOrEmpty(je.GetString()) => null,
                JsonValueKind.Object when je.EnumerateObject().Any() == false => null,
                _ => transformed,
            };
        }

        // empty_to_null: empty arrays/objects → null (scalars keep their own values).
        if (column.Validation?.EmptyToNull == true)
        {
            transformed = transformed switch
            {
                JsonElement { ValueKind: JsonValueKind.Array } arr when arr.GetArrayLength() == 0 => null,
                JsonElement { ValueKind: JsonValueKind.Object } obj when !obj.EnumerateObject().Any() => null,
                System.Collections.IEnumerable { } e when !e.Cast<object?>().Any() => null,
                _ => transformed,
            };
        }

        // Then: validate.
        Validate(transformed, column);

        return transformed;
    }

    /// <summary>
    ///     Applies the configured domain Value Object (e.g. "OneCRef") to the value.
    ///     The Value Object encapsulates parsing/normalization/validation.
    /// </summary>
    private object? ApplyValueObject(object? value, ProfileColumn column)
    {
        var vo = column.Validation?.Vo;
        if (string.IsNullOrWhiteSpace(vo))
        {
            return value;
        }

        if (string.Equals(vo, "OneCRef", StringComparison.Ordinal))
        {
            return OneCRef.FromString(value?.ToString())?.ToString();
        }

        return value;
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
                // Fast Path: Most empty 1C references/objects return empty string or "{}" via String()
                var asString = _session.String(value);
                if (string.IsNullOrEmpty(asString) || asString == "{}")
                {
                    return null;
                }

                // Only if the string looks valid, we might need to check the exact type.
                // But for most cases, if asString is not empty, the value is not Undefined/Null.
                // We remove the expensive ТипЗнч call here and rely on the string representation
                // and the fact that valid objects have a non-empty description or GUID.
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