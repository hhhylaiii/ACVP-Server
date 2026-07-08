using Acvp.WebTool.Api.Models;
using Newtonsoft.Json;
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
    {
        JObject responses;
        try
        {
            responses = JObject.Parse(responsesJson);
        }
        catch (JsonException)
        {
            throw WebToolException.BadRequest(SafeErrorCodes.MalformedUpload,
                "The uploaded file is not valid JSON.",
                hint: "Export responses.json again; compare its structure with example-responses.json.");
        }

        var prompt = JObject.Parse(promptJson);

        RequireMatch(responses, prompt, "vsId");
        RequireMatch(responses, prompt, "algorithm");
        RequireMatch(responses, prompt, "mode");

        if (responses["testGroups"] is not JArray)
        {
            throw WebToolException.BadRequest(SafeErrorCodes.MissingField,
                "The uploaded file has no 'testGroups' array.",
                field: "testGroups",
                hint: "responses.json must mirror the prompt's testGroups/tests structure.");
        }

        return responses;
    }

    private static void RequireMatch(JObject responses, JObject prompt, string property)
    {
        var expected = prompt[property]?.ToString();
        var actual = responses[property]?.ToString();
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw WebToolException.BadRequest(SafeErrorCodes.MismatchedVectorSet,
                $"The uploaded responses belong to a different vector set: '{property}' is '{actual ?? "(missing)"}' but the prompt expects '{expected}'.",
                field: property,
                hint: "Upload the responses produced for exactly this generated prompt.");
        }
    }
}
