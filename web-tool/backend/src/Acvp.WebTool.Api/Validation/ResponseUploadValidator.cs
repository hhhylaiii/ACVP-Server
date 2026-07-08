using Newtonsoft.Json.Linq;

namespace Acvp.WebTool.Api.Validation;

/// <summary>
/// Structural validation of an uploaded responses.json before the engine runs:
/// well-formed JSON, matching vsId/algorithm/mode against the originating prompt
/// (FR-010), and required structure present (FR-008). Never leaks parser internals.
/// </summary>
public sealed class ResponseUploadValidator
{
    /// <summary>Validates the upload against the prompt it claims to answer; returns the parsed document.</summary>
    public JObject Validate(string responsesJson, string promptJson)
        => throw new NotImplementedException();
}
