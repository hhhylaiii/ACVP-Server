using Acvp.WebTool.Api.Models;
using Newtonsoft.Json.Linq;

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
    {
        var registration = new JObject
        {
            ["vsId"] = vsId,
            ["algorithm"] = configuration.Algorithm,
            ["mode"] = configuration.Mode,
            ["revision"] = configuration.Algorithm == "ML-KEM" ? "FIPS203" : "FIPS204",
            ["isSample"] = isSample,
        };

        switch ((configuration.Algorithm, configuration.Mode))
        {
            case ("ML-KEM", "keyGen"):
            case ("ML-DSA", "keyGen"):
                registration["parameterSets"] = new JArray(configuration.ParameterSets);
                break;

            case ("ML-KEM", "encapDecap"):
                registration["parameterSets"] = new JArray(configuration.ParameterSets);
                registration["functions"] = new JArray(
                    "encapsulation", "decapsulation", "encapsulationKeyCheck", "decapsulationKeyCheck");
                break;

            case ("ML-DSA", "sigGen"):
                AddMlDsaSignatureSections(registration, configuration, includeDeterministic: true);
                break;

            case ("ML-DSA", "sigVer"):
                AddMlDsaSignatureSections(registration, configuration, includeDeterministic: false);
                break;

            default:
                throw new ArgumentException(
                    $"Unsupported combination {configuration.Algorithm}/{configuration.Mode}; validate the configuration first.");
        }

        return registration.ToString();
    }

    private static void AddMlDsaSignatureSections(JObject registration, AlgorithmConfiguration configuration, bool includeDeterministic)
    {
        var advanced = configuration.AdvancedOptions;

        registration["capabilities"] = new JArray(new JObject
        {
            ["parameterSets"] = new JArray(configuration.ParameterSets),
            ["messageLength"] = new JArray(new JObject { ["min"] = 8, ["max"] = 1024, ["increment"] = 8 }),
            ["hashAlgs"] = new JArray(advanced?.HashAlgs ?? ["SHA2-256", "SHA2-512", "SHAKE-128", "SHAKE-256"]),
            ["contextLength"] = new JArray(new JObject { ["min"] = 0, ["max"] = 2040, ["increment"] = 8 }),
        });

        if (includeDeterministic)
        {
            registration["deterministic"] = new JArray(advanced?.Deterministic ?? [true, false]);
        }

        registration["externalMu"] = new JArray(advanced?.ExternalMu ?? [true, false]);
        registration["signatureInterfaces"] = new JArray(advanced?.SignatureInterfaces ?? ["external", "internal"]);
        registration["preHash"] = new JArray(advanced?.PreHash ?? ["pure", "preHash"]);
    }
}
