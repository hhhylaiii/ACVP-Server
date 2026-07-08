using Acvp.WebTool.Api.Models;

namespace Acvp.WebTool.Api.Services;

/// <summary>
/// Assembles the downloadable prompt package: prompt.json + a matching example
/// responses.json (derived server-side from expectedResults) + human-readable
/// instructions (FR-003, FR-004). The zip never contains server-only artifacts.
/// </summary>
public sealed class PromptPackageBuilder
{
    public const string PromptEntryName = "prompt.json";
    public const string ExampleResponsesEntryName = "example-responses.json";
    public const string InstructionsEntryName = "INSTRUCTIONS.md";

    /// <summary>Ports the demo harness mapping: answer every prompt case from expectedResults.</summary>
    public string BuildExampleResponses(string promptJson, string expectedResultsJson)
        => throw new NotImplementedException();

    public string BuildInstructions(AlgorithmConfiguration configuration)
        => throw new NotImplementedException();

    public byte[] BuildZip(string promptJson, string exampleResponsesJson, string instructionsMarkdown)
        => throw new NotImplementedException();
}
