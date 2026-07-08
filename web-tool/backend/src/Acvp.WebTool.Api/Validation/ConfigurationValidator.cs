using Acvp.WebTool.Api.Models;

namespace Acvp.WebTool.Api.Validation;

/// <summary>
/// The frozen FIPS 203/204 selection matrix (FR-001, FR-002, FR-015) and the
/// validator that rejects anything outside it.
/// </summary>
public sealed class ConfigurationValidator
{
    /// <summary>Returns the supported selection matrix for the UI.</summary>
    public Capabilities GetCapabilities()
        => throw new NotImplementedException();

    /// <summary>
    /// Throws a <see cref="WebToolException"/> (UNSUPPORTED_SELECTION / INVALID_CONFIGURATION)
    /// when the configuration falls outside the supported matrix.
    /// </summary>
    public void Validate(AlgorithmConfiguration? configuration)
        => throw new NotImplementedException();
}
