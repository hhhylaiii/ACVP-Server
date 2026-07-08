namespace Acvp.WebTool.Api.Models;

/// <summary>
/// Advanced ML-DSA generation options. The MVP applies safe defaults and hides
/// these from non-expert users (FR-002); they are accepted for forward compatibility.
/// </summary>
public sealed record MlDsaAdvancedOptions(
    bool[]? Deterministic = null,
    bool[]? ExternalMu = null,
    string[]? SignatureInterfaces = null,
    string[]? PreHash = null,
    string[]? HashAlgs = null);

/// <summary>
/// The user's algorithm/mode/parameter-set selection that defines a generation request.
/// </summary>
public sealed record AlgorithmConfiguration(
    string Algorithm,
    string Mode,
    string[] ParameterSets,
    MlDsaAdvancedOptions? AdvancedOptions = null);
