using Acvp.WebTool.Api.Models;
using Acvp.WebTool.Api.Services;
using Newtonsoft.Json.Linq;

namespace Acvp.WebTool.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministic in-process stand-in for the Orleans-backed engine so contract
/// tests run without a silo. Shapes mirror the real ACVP artifacts observed in
/// fips-203-204-demo. Golden-parity tests use the real engine instead.
/// </summary>
public sealed class FakeGenValService : IGenValService
{
    /// <summary>Set by tests to hold generate jobs open until released.</summary>
    public TaskCompletionSource? GenerateGate { get; set; }

    public CheckResult CheckParameters(string registrationJson)
    {
        var registration = JObject.Parse(registrationJson);
        return registration["algorithm"] is null
            ? new CheckResult(false, ["registration missing algorithm"])
            : new CheckResult(true, []);
    }

    public async Task<GeneratedVectorSet> GenerateAsync(string registrationJson, long vsId, CancellationToken cancellationToken = default)
    {
        if (GenerateGate is not null)
        {
            await GenerateGate.Task.WaitAsync(cancellationToken);
        }

        var registration = JObject.Parse(registrationJson);
        var algorithm = registration.Value<string>("algorithm")!;
        var mode = registration.Value<string>("mode")!;
        var revision = registration.Value<string>("revision")!;

        var (promptFields, answerFields) = SampleFieldsFor(algorithm, mode);

        JArray MakeGroup(Func<int, JObject> makeTest) =>
        [
            new JObject
            {
                ["tgId"] = 1,
                ["testType"] = "AFT",
                ["parameterSet"] = registration["parameterSets"]?.First?.ToString() ?? "ML-KEM-768",
                ["tests"] = new JArray(Enumerable.Range(1, 3).Select(makeTest)),
            },
        ];

        var prompt = new JObject
        {
            ["vsId"] = vsId,
            ["algorithm"] = algorithm,
            ["mode"] = mode,
            ["revision"] = revision,
            ["isSample"] = true,
            ["testGroups"] = MakeGroup(tcId =>
            {
                var test = new JObject { ["tcId"] = tcId };
                foreach (var field in promptFields)
                {
                    test[field] = $"{field}-{tcId:x2}";
                }
                return test;
            }),
        };

        var expected = new JObject
        {
            ["vsId"] = vsId,
            ["algorithm"] = algorithm,
            ["mode"] = mode,
            ["revision"] = revision,
            ["isSample"] = true,
            ["testGroups"] = MakeGroup(tcId =>
            {
                var test = new JObject { ["tcId"] = tcId };
                foreach (var field in answerFields)
                {
                    test[field] = $"{field}-answer-{tcId:x2}";
                }
                return test;
            }),
        };

        var internalProjection = new JObject(prompt) { ["internal"] = true };

        return new GeneratedVectorSet(
            prompt.ToString(),
            internalProjection.ToString(),
            expected.ToString());
    }

    public Task<string> ValidateAsync(string internalProjectionJson, string responsesJson, long vsId, CancellationToken cancellationToken = default)
    {
        var responses = JObject.Parse(responsesJson);
        var tests = new JArray();
        foreach (var group in responses["testGroups"] ?? new JArray())
        {
            foreach (var test in group["tests"] ?? new JArray())
            {
                var tcId = test.Value<int>("tcId");
                var failed = test.Value<bool?>("__forceFail") == true;
                var entry = new JObject
                {
                    ["tcId"] = tcId,
                    ["result"] = failed ? "failed" : "passed",
                };
                if (failed)
                {
                    entry["reason"] = "Incorrect answer";
                }
                tests.Add(entry);
            }
        }

        var anyFailed = tests.Any(t => t.Value<string>("result") == "failed");
        var validation = new JObject
        {
            ["vsId"] = vsId,
            ["disposition"] = anyFailed ? "failed" : "passed",
            ["tests"] = tests,
        };

        return Task.FromResult(validation.ToString());
    }

    private static (string[] PromptFields, string[] AnswerFields) SampleFieldsFor(string algorithm, string mode)
        => (algorithm, mode) switch
        {
            ("ML-KEM", "keyGen") => (["d", "z"], ["ek", "dk"]),
            ("ML-KEM", "encapDecap") => (["ek", "m"], ["c", "k"]),
            ("ML-DSA", "keyGen") => (["seed"], ["pk", "sk"]),
            ("ML-DSA", "sigGen") => (["message", "rnd"], ["signature"]),
            ("ML-DSA", "sigVer") => (["message", "signature", "pk"], ["testPassed"]),
            _ => (["input"], ["output"]),
        };
}
