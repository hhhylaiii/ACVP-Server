using Acvp.WebTool.Api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Acvp.WebTool.Api.Validation;

/// <summary>
/// Structural validation of an uploaded responses.json before the engine runs:
/// well-formed JSON, matching vsId/algorithm/mode against the originating prompt
/// (FR-010), known tcIds and required per-mode response fields present with
/// precise tcId/field/hint errors (FR-008). Never leaks parser internals.
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

        if (responses["testGroups"] is not JArray responseGroups)
        {
            throw WebToolException.BadRequest(SafeErrorCodes.MissingField,
                "The uploaded file has no 'testGroups' array.",
                field: "testGroups",
                hint: "responses.json must mirror the prompt's testGroups/tests structure.");
        }

        ValidateTests(responseGroups, prompt);
        return responses;
    }

    private static void ValidateTests(JArray responseGroups, JObject prompt)
    {
        var algorithm = prompt.Value<string>("algorithm") ?? string.Empty;
        var mode = prompt.Value<string>("mode") ?? string.Empty;

        var knownTcIds = new HashSet<int>();
        var functionByTgId = new Dictionary<int, string?>();
        foreach (var group in prompt["testGroups"] ?? new JArray())
        {
            functionByTgId[group.Value<int>("tgId")] = group.Value<string>("function");
            foreach (var test in group["tests"] ?? new JArray())
            {
                knownTcIds.Add(test.Value<int>("tcId"));
            }
        }

        foreach (var group in responseGroups)
        {
            var tgId = group.Value<int>("tgId");
            var requiredFields = RequiredFieldsFor(algorithm, mode, functionByTgId.GetValueOrDefault(tgId));

            foreach (var test in group["tests"] ?? new JArray())
            {
                if (test["tcId"] is null)
                {
                    throw WebToolException.BadRequest(SafeErrorCodes.MissingField,
                        "A test case in the upload has no 'tcId'.",
                        field: "tcId",
                        hint: "Every answered test case must echo the tcId from prompt.json.");
                }

                var tcId = test.Value<int>("tcId");
                if (!knownTcIds.Contains(tcId))
                {
                    throw WebToolException.BadRequest(SafeErrorCodes.UnknownTcId,
                        $"Test case tcId {tcId} does not exist in the generated prompt.",
                        tcId: tcId,
                        hint: "Only answer the tcIds present in prompt.json; remove any extras.");
                }

                foreach (var field in requiredFields)
                {
                    if (IsMissing(test[field]))
                    {
                        throw WebToolException.BadRequest(SafeErrorCodes.MissingField,
                            $"Test case tcId {tcId} is missing the required field '{field}'.",
                            tcId: tcId,
                            field: field,
                            hint: HintFor(field));
                    }
                }
            }
        }
    }

    private static bool IsMissing(JToken? token)
        => token is null
           || token.Type == JTokenType.Null
           || (token.Type == JTokenType.String && string.IsNullOrEmpty(token.Value<string>()));

    /// <summary>The response fields the engine's validators read for each mode (see field-mapping.md).</summary>
    private static string[] RequiredFieldsFor(string algorithm, string mode, string? function)
        => (algorithm, mode) switch
        {
            ("ML-KEM", "keyGen") => ["ek", "dk"],
            ("ML-KEM", "encapDecap") => function switch
            {
                "encapsulation" => ["c", "k"],
                "decapsulation" => ["k"],
                "encapsulationKeyCheck" or "decapsulationKeyCheck" => ["testPassed"],
                _ => [],
            },
            ("ML-DSA", "keyGen") => ["pk", "sk"],
            ("ML-DSA", "sigGen") => ["signature"],
            ("ML-DSA", "sigVer") => ["testPassed"],
            _ => [],
        };

    private static string HintFor(string field)
        => field == "testPassed"
            ? "Provide 'testPassed' as a JSON boolean (true/false)."
            : $"Provide '{field}' as a hex string for every test case; see field-mapping.md for its byte length.";

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
