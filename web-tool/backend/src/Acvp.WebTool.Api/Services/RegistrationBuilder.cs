using Acvp.WebTool.Api.Models;

namespace Acvp.WebTool.Api.Services;

/// <summary>
/// Translates a validated <see cref="AlgorithmConfiguration"/> into the ACVP
/// registration JSON consumed by the engine. ML-DSA advanced options receive
/// safe defaults when omitted (FR-002); the defaults mirror the proven
/// fips-203-204-demo registrations.
/// </summary>
public sealed class RegistrationBuilder
{
    public string Build(AlgorithmConfiguration configuration, long vsId, bool isSample)
        => throw new NotImplementedException();
}
