using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Validation;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.UnitTests;

/// <summary>
/// T036 — a response file that does not belong to the prompt's vector set is
/// detected and reported (MISMATCHED_VECTORSET), never silently graded (FR-010).
/// </summary>
public sealed class UploadMismatchTests
{
    private readonly ResponseUploadValidator _validator = new();

    private const string Prompt = """
        {
          "vsId": 42,
          "algorithm": "ML-KEM",
          "mode": "keyGen",
          "revision": "FIPS203",
          "testGroups": [
            { "tgId": 1, "tests": [ { "tcId": 1, "d": "aa", "z": "bb" }, { "tcId": 2, "d": "cc", "z": "dd" } ] }
          ]
        }
        """;

    private static string Responses(long vsId = 42, string algorithm = "ML-KEM", string mode = "keyGen")
        => $$"""
        {
          "vsId": {{vsId}},
          "algorithm": "{{algorithm}}",
          "mode": "{{mode}}",
          "revision": "FIPS203",
          "testGroups": [
            { "tgId": 1, "tests": [ { "tcId": 1, "ek": "11", "dk": "22" }, { "tcId": 2, "ek": "33", "dk": "44" } ] }
          ]
        }
        """;

    [Fact]
    public void Validate_MatchingUpload_Passes()
    {
        var act = () => _validator.Validate(Responses(), Prompt);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DifferentVsId_ThrowsMismatchedVectorSet()
    {
        var act = () => _validator.Validate(Responses(vsId: 999), Prompt);

        act.Should().Throw<WebToolException>()
            .Which.Error.Code.Should().Be(SafeErrorCodes.MismatchedVectorSet);
    }

    [Fact]
    public void Validate_DifferentAlgorithm_ThrowsMismatchedVectorSet()
    {
        var act = () => _validator.Validate(Responses(algorithm: "ML-DSA"), Prompt);

        act.Should().Throw<WebToolException>()
            .Which.Error.Code.Should().Be(SafeErrorCodes.MismatchedVectorSet);
    }

    [Fact]
    public void Validate_DifferentMode_ThrowsMismatchedVectorSet()
    {
        var act = () => _validator.Validate(Responses(mode: "encapDecap"), Prompt);

        act.Should().Throw<WebToolException>()
            .Which.Error.Code.Should().Be(SafeErrorCodes.MismatchedVectorSet);
    }

    [Fact]
    public void Validate_MissingTestGroups_ThrowsMissingField()
    {
        const string noGroups = """{ "vsId": 42, "algorithm": "ML-KEM", "mode": "keyGen" }""";

        var act = () => _validator.Validate(noGroups, Prompt);

        var error = act.Should().Throw<WebToolException>().Which.Error;
        error.Code.Should().Be(SafeErrorCodes.MissingField);
        error.Field.Should().Be("testGroups");
    }

    [Fact]
    public void Validate_MalformedJson_ThrowsSafeErrorWithoutParserInternals()
    {
        var act = () => _validator.Validate("{ this is not json", Prompt);

        var error = act.Should().Throw<WebToolException>().Which.Error;
        error.Code.Should().Be(SafeErrorCodes.MalformedUpload);
        error.Message.Should().NotContain("Newtonsoft", "parser internals must not leak");
        error.Message.Should().NotContain("   at ", "no stack traces in safe errors");
    }
}
