using Acvp.WebTool.Api.Services;
using FluentAssertions;
using Xunit;

namespace Acvp.WebTool.Api.UnitTests;

/// <summary>T040 — validation.json → ValidationReport with verbatim disposition (FR-006/FR-007).</summary>
public sealed class ValidationReportBuilderTests
{
    private readonly ValidationReportBuilder _builder = new();

    [Fact]
    public void Build_AllPassed_ReportsPassedWithCounts()
    {
        const string validation = """
            {
              "vsId": 42,
              "disposition": "passed",
              "tests": [
                { "tcId": 1, "result": "passed" },
                { "tcId": 2, "result": "passed" }
              ]
            }
            """;

        var report = _builder.Build("job-1", validation);

        report.JobId.Should().Be("job-1");
        report.Disposition.Should().Be("passed");
        report.Summary.Total.Should().Be(2);
        report.Summary.Passed.Should().Be(2);
        report.Summary.Failed.Should().Be(0);
        report.Cases.Should().HaveCount(2);
        report.Cases.Should().OnlyContain(c => c.Passed);
    }

    [Fact]
    public void Build_OneFailure_ReportsFailedAndHighlightsCase()
    {
        const string validation = """
            {
              "vsId": 42,
              "disposition": "failed",
              "tests": [
                { "tcId": 1, "result": "passed" },
                { "tcId": 2, "result": "failed", "reason": "EK does not match" },
                { "tcId": 3, "result": "passed" }
              ]
            }
            """;

        var report = _builder.Build("job-2", validation);

        report.Disposition.Should().Be("failed");
        report.Summary.Total.Should().Be(3);
        report.Summary.Passed.Should().Be(2);
        report.Summary.Failed.Should().Be(1);

        var failing = report.Cases.Single(c => !c.Passed);
        failing.TcId.Should().Be(2);
        failing.Reason.Should().Be("EK does not match");
    }

    [Fact]
    public void Build_MissingResult_CountsAsFailed()
    {
        const string validation = """
            {
              "vsId": 42,
              "disposition": "missing",
              "tests": [ { "tcId": 1, "result": "missing", "reason": "no answer provided" } ]
            }
            """;

        var report = _builder.Build("job-3", validation);

        report.Disposition.Should().Be("missing");
        report.Summary.Failed.Should().Be(1);
        report.Cases.Single().Passed.Should().BeFalse();
    }
}
