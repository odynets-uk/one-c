using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OneC.Domain.Profiles;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Maps COM values from 1C to .NET types and applies transformations and validation.
/// </summary>
public sealed class ComValueMapper
{
    private readonly ILogger<ComValueMapper> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComValueMapper" /> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ComValueMapper(ILogger<ComValueMapper> logger)
    {
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
        // First: transform the raw value.
        var transformed = ApplyTransform(rawValue, column.Transform);

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