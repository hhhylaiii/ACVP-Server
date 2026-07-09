using Acvp.WebTool.Api.Models;
using Newtonsoft.Json.Linq;

namespace Acvp.WebTool.Api.Services;

/// <summary>
/// Builds the human-readable <see cref="ValidationReport"/> from the engine's
/// validation.json. The disposition is passed through verbatim (FR-007).
/// </summary>
public sealed class ValidationReportBuilder
{
    public ValidationReport Build(string jobId, string validationJson)
    {
        var validation = JObject.Parse(validationJson);
        var disposition = validation.Value<string>("disposition") ?? "failed";

        var cases = (validation["tests"] as JArray ?? [])
            .Select(test => new ValidationCase(
                TcId: test.Value<int>("tcId"),
                Passed: string.Equals(test.Value<string>("result"), "passed", StringComparison.Ordinal),
                Reason: test.Value<string>("reason")))
            .ToArray();

        var passed = cases.Count(c => c.Passed);
        var summary = new ValidationSummary(cases.Length, passed, cases.Length - passed);

        return new ValidationReport(jobId, disposition, summary, cases);
    }
}
