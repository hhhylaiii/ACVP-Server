using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Validation;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.UnitTests;

/// <summary>
/// T022 — the configuration validator accepts exactly the in-scope matrix and
/// rejects everything else with UNSUPPORTED_SELECTION / INVALID_CONFIGURATION.
/// </summary>
public sealed class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator = new();

    [Theory]
    [InlineData("ML-KEM", "keyGen", "ML-KEM-512")]
    [InlineData("ML-KEM", "keyGen", "ML-KEM-768")]
    [InlineData("ML-KEM", "keyGen", "ML-KEM-1024")]
    [InlineData("ML-KEM", "encapDecap", "ML-KEM-768")]
    [InlineData("ML-DSA", "keyGen", "ML-DSA-44")]
    [InlineData("ML-DSA", "sigGen", "ML-DSA-65")]
    [InlineData("ML-DSA", "sigVer", "ML-DSA-87")]
    public void Validate_InScopeCombinations_Pass(string algorithm, string mode, string parameterSet)
    {
        var act = () => _validator.Validate(new AlgorithmConfiguration(algorithm, mode, [parameterSet]));

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_AllParameterSetsAtOnce_Passes()
    {
        var act = () => _validator.Validate(new AlgorithmConfiguration(
            "ML-KEM", "encapDecap", ["ML-KEM-512", "ML-KEM-768", "ML-KEM-1024"]));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("ML-KEM", "sigGen", "ML-KEM-768")]     // mode belongs to ML-DSA
    [InlineData("ML-KEM", "sigVer", "ML-KEM-768")]
    [InlineData("ML-DSA", "encapDecap", "ML-DSA-65")]  // mode belongs to ML-KEM
    [InlineData("ML-KEM", "keyGen", "ML-DSA-65")]      // parameter set of the other family
    [InlineData("ML-DSA", "keyGen", "ML-KEM-768")]
    [InlineData("RSA", "keyGen", "ML-KEM-768")]        // out-of-scope algorithm
    [InlineData("SLH-DSA", "keyGen", "SLH-DSA-128s")]
    public void Validate_OutOfScopeCombinations_ThrowUnsupportedSelection(string algorithm, string mode, string parameterSet)
    {
        var act = () => _validator.Validate(new AlgorithmConfiguration(algorithm, mode, [parameterSet]));

        act.Should().Throw<WebToolException>()
            .Which.Error.Code.Should().Be(SafeErrorCodes.UnsupportedSelection);
    }

    [Fact]
    public void Validate_NullConfiguration_ThrowsInvalidConfiguration()
    {
        var act = () => _validator.Validate(null);

        act.Should().Throw<WebToolException>()
            .Which.Error.Code.Should().Be(SafeErrorCodes.InvalidConfiguration);
    }

    [Fact]
    public void Validate_EmptyParameterSets_Throws()
    {
        var act = () => _validator.Validate(new AlgorithmConfiguration("ML-KEM", "keyGen", []));

        act.Should().Throw<WebToolException>();
    }

    [Fact]
    public void Validate_MixedFamilies_Throws()
    {
        var act = () => _validator.Validate(new AlgorithmConfiguration(
            "ML-KEM", "keyGen", ["ML-KEM-768", "ML-DSA-65"]));

        act.Should().Throw<WebToolException>()
            .Which.Error.Code.Should().Be(SafeErrorCodes.UnsupportedSelection);
    }

    [Fact]
    public void GetCapabilities_MatchesFrozenScope()
    {
        var capabilities = _validator.GetCapabilities();

        capabilities.Algorithms.Should().HaveCount(2);
        capabilities.Algorithms.Select(a => a.Algorithm).Should().BeEquivalentTo("ML-KEM", "ML-DSA");
    }
}
