using Acvp.WebTool.Api.Models;

namespace Acvp.WebTool.Api.Services;

/// <summary>
/// Builds the human-readable <see cref="ValidationReport"/> from the engine's
/// validation.json. The disposition is passed through verbatim (FR-007).
/// </summary>
public sealed class ValidationReportBuilder
{
    public ValidationReport Build(string jobId, string validationJson)
        => throw new NotImplementedException();
}
