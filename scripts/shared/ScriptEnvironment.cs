using System.Globalization;

namespace Template.Scripting;

/// <summary>
/// Reads and normalizes environment values used by file-based scripts.
/// </summary>
/// <remarks>
/// This helper intentionally keeps environment access centralized so missing values and defaults produce consistent diagnostic messages.
/// Values are read from the current process environment at call time.
/// </remarks>
public static class ScriptEnvironment
{
    /// <summary>
    /// Reads a required environment variable.
    /// </summary>
    /// <param name="name">Environment variable name.</param>
    /// <returns>The non-empty environment variable value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    /// <exception cref="ScriptConfigurationException">Thrown when the environment variable is missing or empty.</exception>
    public static string Require(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment variable name cannot be empty.", nameof(name));
        }

        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new ScriptConfigurationException($"Environment variable '{name}' is not set or is empty. Set '{name}' before running this repository automation script.")
            : value;
    }

    /// <summary>
    /// Reads an optional environment variable and returns a default when it is missing, empty, or whitespace-only.
    /// </summary>
    /// <param name="name">Environment variable name.</param>
    /// <param name="defaultValue">Value returned when the environment variable is missing, empty, or whitespace-only.</param>
    /// <returns>The configured environment value, or <paramref name="defaultValue"/> when the environment value is missing, empty, or whitespace-only.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    public static string GetOrDefault(string name, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment variable name cannot be empty.", nameof(name));
        }

        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    /// <summary>
    /// Interprets a common truthy environment value.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns><see langword="true"/> for <c>true</c>, <c>1</c>, or <c>yes</c>, otherwise <see langword="false"/>.</returns>
    public static bool IsEnabled(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Converts text to lowercase with invariant-culture rules.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Lowercase text using <see cref="CultureInfo.InvariantCulture"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string LowerInvariant(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.ToLower(CultureInfo.InvariantCulture);
    }
}
