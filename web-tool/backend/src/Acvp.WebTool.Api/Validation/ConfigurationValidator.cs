using Acvp.WebTool.Api.Models;

namespace Acvp.WebTool.Api.Validation;

/// <summary>
/// The frozen FIPS 203/204 selection matrix (FR-001, FR-002, FR-015) and the
/// validator that rejects anything outside it.
/// </summary>
public sealed class ConfigurationValidator
{
    private static readonly IReadOnlyDictionary<string, (string[] Modes, string[] ParameterSets)> Matrix =
        new Dictionary<string, (string[], string[])>(StringComparer.Ordinal)
        {
            ["ML-KEM"] = (["keyGen", "encapDecap"], ["ML-KEM-512", "ML-KEM-768", "ML-KEM-1024"]),
            ["ML-DSA"] = (["keyGen", "sigGen", "sigVer"], ["ML-DSA-44", "ML-DSA-65", "ML-DSA-87"]),
        };

    /// <summary>Returns the supported selection matrix for the UI.</summary>
    public Capabilities GetCapabilities()
        => new(Matrix
            .Select(entry => new AlgorithmCapability(entry.Key, entry.Value.Modes, entry.Value.ParameterSets))
            .ToArray());

    /// <summary>
    /// Throws a <see cref="WebToolException"/> (UNSUPPORTED_SELECTION / INVALID_CONFIGURATION)
    /// when the configuration falls outside the supported matrix.
    /// </summary>
    public void Validate(AlgorithmConfiguration? configuration)
    {
        if (configuration is null
            || string.IsNullOrWhiteSpace(configuration.Algorithm)
            || string.IsNullOrWhiteSpace(configuration.Mode))
        {
            throw WebToolException.BadRequest(SafeErrorCodes.InvalidConfiguration,
                "The request must include an algorithm, a mode and at least one parameter set.");
        }

        if (!Matrix.TryGetValue(configuration.Algorithm, out var supported))
        {
            throw WebToolException.BadRequest(SafeErrorCodes.UnsupportedSelection,
                $"Algorithm '{configuration.Algorithm}' is not supported. Supported algorithms: {string.Join(", ", Matrix.Keys)}.");
        }

        if (!supported.Modes.Contains(configuration.Mode, StringComparer.Ordinal))
        {
            throw WebToolException.BadRequest(SafeErrorCodes.UnsupportedSelection,
                $"Mode '{configuration.Mode}' is not supported for {configuration.Algorithm}. Supported modes: {string.Join(", ", supported.Modes)}.");
        }

        if (configuration.ParameterSets is not { Length: > 0 })
        {
            throw WebToolException.BadRequest(SafeErrorCodes.InvalidConfiguration,
                $"Select at least one parameter set. Supported: {string.Join(", ", supported.ParameterSets)}.");
        }

        var invalid = configuration.ParameterSets
            .Where(ps => !supported.ParameterSets.Contains(ps, StringComparer.Ordinal))
            .ToArray();
        if (invalid.Length > 0)
        {
            throw WebToolException.BadRequest(SafeErrorCodes.UnsupportedSelection,
                $"Parameter set(s) {string.Join(", ", invalid)} are not valid for {configuration.Algorithm}. Supported: {string.Join(", ", supported.ParameterSets)}.");
        }
    }
}
