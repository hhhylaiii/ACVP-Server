using System.Diagnostics;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests;

/// <summary>
/// T046 — FR-014/SC-006: the integration pack's sample harnesses produce a valid
/// responses.json for their algorithm family (only the module-invocation line is
/// left to fill; in demo mode they answer from expectedResults), and the
/// field-mapping documentation covers every supported mode.
/// </summary>
public sealed class SupportPackTests
{
    private static readonly string IntegrationPackDir =
        Path.Combine(FindRepoRoot(), "web-tool", "integration-pack");

    [SkippableTheory]
    [InlineData("harness_mlkem.py", "ML-KEM", "keyGen", "FIPS203", new[] { "ek", "dk" }, new[] { "d", "z" })]
    [InlineData("harness_mldsa.py", "ML-DSA", "keyGen", "FIPS204", new[] { "pk", "sk" }, new[] { "seed" })]
    public async Task Harness_DemoMode_ProducesValidResponses(
        string harness, string algorithm, string mode, string revision,
        string[] answerFields, string[] promptFields)
    {
        SkipUnlessPythonAvailable();

        var workDir = Path.Combine(Path.GetTempPath(), $"acvp-harness-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        try
        {
            WriteFixture(workDir, algorithm, mode, revision, promptFields, answerFields);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{Path.Combine(IntegrationPackDir, harness)}\" \"{Path.Combine(workDir, "prompt.json")}\"",
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
            process.ExitCode.Should().Be(0, $"harness stderr: {stderr}");

            var responses = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(workDir, "responses.json")));
            responses.Value<long>("vsId").Should().Be(42);
            responses.Value<string>("algorithm").Should().Be(algorithm);
            var tests = responses["testGroups"]!.SelectMany(g => g["tests"]!).ToArray();
            tests.Should().HaveCount(2);
            foreach (var test in tests)
            {
                foreach (var field in answerFields)
                {
                    test[field]!.ToString().Should().NotBeNullOrWhiteSpace(
                        $"the harness must fill '{field}' for every test case");
                }
            }
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    [Fact]
    public async Task FieldMappingDoc_CoversEverySupportedMode()
    {
        var path = Path.Combine(IntegrationPackDir, "field-mapping.md");
        File.Exists(path).Should().BeTrue("field-mapping.md is a required deliverable (FR-014)");
        var doc = await File.ReadAllTextAsync(path);

        // Every mode section and its required response fields must be documented.
        foreach (var expected in new[]
                 {
                     "ML-KEM keyGen", "ML-KEM encapDecap",
                     "ML-DSA keyGen", "ML-DSA sigGen", "ML-DSA sigVer",
                     "`ek`", "`dk`", "`c`", "`k`", "`pk`", "`sk`", "`signature`", "`testPassed`",
                 })
        {
            doc.Should().Contain(expected);
        }
    }

    [Theory]
    [InlineData("mlkem_keygen_responses.json", "ML-KEM")]
    [InlineData("mlkem_encapdecap_responses.json", "ML-KEM")]
    [InlineData("mldsa_keygen_responses.json", "ML-DSA")]
    [InlineData("mldsa_siggen_responses.json", "ML-DSA")]
    [InlineData("mldsa_sigver_responses.json", "ML-DSA")]
    public async Task SampleResponses_ExistAndParse(string fileName, string algorithm)
    {
        var path = Path.Combine(IntegrationPackDir, "sample-responses", fileName);
        File.Exists(path).Should().BeTrue($"{fileName} is part of the support pack (FR-014)");

        var sample = JObject.Parse(await File.ReadAllTextAsync(path));
        sample.Value<string>("algorithm").Should().Be(algorithm);
        sample["testGroups"].Should().NotBeNull();
    }

    private static void WriteFixture(
        string dir, string algorithm, string mode, string revision, string[] promptFields, string[] answerFields)
    {
        JObject MakeDoc(string[] fields, string prefix)
        {
            var tests = new JArray(Enumerable.Range(1, 2).Select(tcId =>
            {
                var test = new JObject { ["tcId"] = tcId };
                foreach (var field in fields)
                {
                    test[field] = $"{prefix}{tcId:x2}";
                }
                return test;
            }));
            return new JObject
            {
                ["vsId"] = 42,
                ["algorithm"] = algorithm,
                ["mode"] = mode,
                ["revision"] = revision,
                ["isSample"] = true,
                ["testGroups"] = new JArray(new JObject
                {
                    ["tgId"] = 1,
                    ["testType"] = "AFT",
                    ["parameterSet"] = algorithm == "ML-KEM" ? "ML-KEM-768" : "ML-DSA-65",
                    ["tests"] = tests,
                }),
            };
        }

        File.WriteAllText(Path.Combine(dir, "prompt.json"), MakeDoc(promptFields, "aa").ToString());
        File.WriteAllText(Path.Combine(dir, "expectedResults.json"), MakeDoc(answerFields, "bb").ToString());
    }

    private static void SkipUnlessPythonAvailable()
    {
        try
        {
            var probe = Process.Start(new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            probe.WaitForExit(5000);
            Skip.If(probe.ExitCode != 0, "python3 is not available");
        }
        catch (Exception)
        {
            Skip.If(true, "python3 is not available");
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "web-tool")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (web-tool directory).");
    }
}
