using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Services;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Acvp.WebTool.Api.UnitTests;

/// <summary>
/// T022/T025 — AlgorithmConfiguration → ACVP registration translation, including
/// ML-DSA safe defaults matching the proven fips-203-204-demo registrations.
/// </summary>
public sealed class RegistrationBuilderTests
{
    private readonly RegistrationBuilder _builder = new();

    [Fact]
    public void Build_MlKemKeyGen_ProducesFips203Registration()
    {
        var json = _builder.Build(new AlgorithmConfiguration("ML-KEM", "keyGen", ["ML-KEM-768"]), vsId: 42, isSample: true);
        var registration = JObject.Parse(json);

        registration.Value<long>("vsId").Should().Be(42);
        registration.Value<string>("algorithm").Should().Be("ML-KEM");
        registration.Value<string>("mode").Should().Be("keyGen");
        registration.Value<string>("revision").Should().Be("FIPS203");
        registration.Value<bool>("isSample").Should().BeTrue();
        registration["parameterSets"]!.Values<string>().Should().BeEquivalentTo("ML-KEM-768");
    }

    [Fact]
    public void Build_MlKemEncapDecap_IncludesAllFunctions()
    {
        var json = _builder.Build(new AlgorithmConfiguration("ML-KEM", "encapDecap", ["ML-KEM-512"]), 1, true);
        var registration = JObject.Parse(json);

        registration["functions"]!.Values<string>().Should().BeEquivalentTo(
            "encapsulation", "decapsulation", "encapsulationKeyCheck", "decapsulationKeyCheck");
    }

    [Fact]
    public void Build_MlDsaKeyGen_ProducesFips204Registration()
    {
        var json = _builder.Build(new AlgorithmConfiguration("ML-DSA", "keyGen", ["ML-DSA-44", "ML-DSA-87"]), 7, true);
        var registration = JObject.Parse(json);

        registration.Value<string>("revision").Should().Be("FIPS204");
        registration["parameterSets"]!.Values<string>().Should().BeEquivalentTo("ML-DSA-44", "ML-DSA-87");
    }

    [Fact]
    public void Build_MlDsaSigGen_AppliesSafeDefaults()
    {
        var json = _builder.Build(new AlgorithmConfiguration("ML-DSA", "sigGen", ["ML-DSA-65"]), 7, true);
        var registration = JObject.Parse(json);

        var capability = (JObject)registration["capabilities"]![0]!;
        capability["parameterSets"]!.Values<string>().Should().BeEquivalentTo("ML-DSA-65");
        capability["messageLength"].Should().NotBeNull();
        capability["hashAlgs"]!.Values<string>().Should().Contain("SHA2-256");
        capability["contextLength"].Should().NotBeNull();

        registration["deterministic"]!.Values<bool>().Should().BeEquivalentTo([true, false]);
        registration["externalMu"]!.Values<bool>().Should().BeEquivalentTo([true, false]);
        registration["signatureInterfaces"]!.Values<string>().Should().BeEquivalentTo("external", "internal");
        registration["preHash"]!.Values<string>().Should().BeEquivalentTo("pure", "preHash");
    }

    [Fact]
    public void Build_MlDsaSigVer_AppliesSafeDefaultsWithoutDeterministic()
    {
        var json = _builder.Build(new AlgorithmConfiguration("ML-DSA", "sigVer", ["ML-DSA-87"]), 7, true);
        var registration = JObject.Parse(json);

        registration["capabilities"].Should().NotBeNull();
        registration["deterministic"].Should().BeNull("the demo sigVer registration carries no deterministic flag");
        registration["externalMu"]!.Values<bool>().Should().BeEquivalentTo([true, false]);
    }

    [Fact]
    public void Build_MlDsaSigGen_AdvancedOptionsOverrideDefaults()
    {
        var config = new AlgorithmConfiguration("ML-DSA", "sigGen", ["ML-DSA-65"],
            new MlDsaAdvancedOptions(
                Deterministic: [true],
                ExternalMu: [false],
                SignatureInterfaces: ["external"],
                PreHash: ["pure"]));

        var registration = JObject.Parse(_builder.Build(config, 7, true));

        registration["deterministic"]!.Values<bool>().Should().BeEquivalentTo([true]);
        registration["externalMu"]!.Values<bool>().Should().BeEquivalentTo([false]);
        registration["signatureInterfaces"]!.Values<string>().Should().BeEquivalentTo("external");
        registration["preHash"]!.Values<string>().Should().BeEquivalentTo("pure");
    }
}
