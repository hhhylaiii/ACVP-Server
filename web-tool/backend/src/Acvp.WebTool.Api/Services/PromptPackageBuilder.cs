using System.IO.Compression;
using System.Text;
using Acvp.WebTool.Api.Models;
using Newtonsoft.Json.Linq;

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

    /// <summary>
    /// Ports the demo harness mapping: answer every prompt case with the answer
    /// fields from expectedResults, keyed by (tgId, tcId). Produces a response
    /// file that is both a format reference and a passing happy-path upload.
    /// </summary>
    public string BuildExampleResponses(string promptJson, string expectedResultsJson)
    {
        var prompt = JObject.Parse(promptJson);
        var expected = JObject.Parse(expectedResultsJson);

        var expectedByKey = new Dictionary<(int TgId, int TcId), JObject>();
        foreach (var group in expected["testGroups"] ?? new JArray())
        {
            var tgId = group.Value<int>("tgId");
            foreach (var test in group["tests"] ?? new JArray())
            {
                expectedByKey[(tgId, test.Value<int>("tcId"))] = (JObject)test;
            }
        }

        var outGroups = new JArray();
        foreach (var group in prompt["testGroups"] ?? new JArray())
        {
            var tgId = group.Value<int>("tgId");
            var outTests = new JArray();
            foreach (var test in group["tests"] ?? new JArray())
            {
                var tcId = test.Value<int>("tcId");
                if (!expectedByKey.TryGetValue((tgId, tcId), out var answer))
                {
                    continue;
                }

                var outTest = new JObject { ["tcId"] = tcId };
                foreach (var property in answer.Properties().Where(p => p.Name != "tcId"))
                {
                    outTest[property.Name] = property.Value.DeepClone();
                }

                outTests.Add(outTest);
            }

            outGroups.Add(new JObject { ["tgId"] = tgId, ["tests"] = outTests });
        }

        var responses = new JObject
        {
            ["vsId"] = prompt["vsId"]?.DeepClone(),
            ["algorithm"] = prompt["algorithm"]?.DeepClone(),
            ["revision"] = prompt["revision"]?.DeepClone(),
        };

        foreach (var optional in new[] { "mode", "isSample" })
        {
            if (prompt[optional] is { } value)
            {
                responses[optional] = value.DeepClone();
            }
        }

        responses["testGroups"] = outGroups;
        return responses.ToString();
    }

    public string BuildInstructions(AlgorithmConfiguration configuration)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# 如何作答（How to produce responses.json）— {configuration.Algorithm} / {configuration.Mode}");
        builder.AppendLine();
        builder.AppendLine($"參數集（parameter sets）：{string.Join(", ", configuration.ParameterSets)}");
        builder.AppendLine();
        builder.AppendLine("## 步驟 (Steps)");
        builder.AppendLine();
        builder.AppendLine("1. 將 `prompt.json` 交給貴公司的工程師。`prompt.json` 內含多個測試題目，");
        builder.AppendLine("   每一題都有一個 `tcId`（test case id）。");
        builder.AppendLine("   Hand `prompt.json` to your engineer. It contains the test questions; every");
        builder.AppendLine("   case is identified by a `tcId`.");
        builder.AppendLine("2. 使用貴公司的加密模組（IUT）回答每一題，把答案寫成 `responses.json`。");
        builder.AppendLine("   格式請參考隨附的 `example-responses.json`（欄位與結構完全相同，僅數值不同）。");
        builder.AppendLine("   Answer every case with your own cryptographic module and write the answers");
        builder.AppendLine("   as `responses.json`, following the structure of `example-responses.json`.");
        builder.AppendLine("3. 建議使用整合支援包（integration pack）中的範例程式 `harness_mlkem.py` /");
        builder.AppendLine("   `harness_mldsa.py`：只需填入「呼叫貴模組」的那一行即可產生正確格式。");
        builder.AppendLine("   The integration pack ships harnesses where only the module-invocation line");
        builder.AppendLine("   needs to be filled in.");
        builder.AppendLine("4. 回到網頁工具的「上傳作答」頁面，上傳 `responses.json`，即可取得逐題批改報告。");
        builder.AppendLine("   Upload `responses.json` on the tool's upload page to receive the pass/fail report.");
        builder.AppendLine();
        builder.AppendLine("## 注意事項 (Notes)");
        builder.AppendLine();
        builder.AppendLine("- `responses.json` 的 `vsId`、`algorithm` 必須與 `prompt.json` 相同，否則會被判定為不相符。");
        builder.AppendLine("  Keep `vsId` and `algorithm` identical to the prompt or the upload is rejected as mismatched.");
        builder.AppendLine("- 所有二進位值一律使用十六進位字串（hex）表示。All binary values are hex strings.");
        builder.AppendLine("- 本工具絕不要求、也絕不儲存模組原始碼或私鑰。");
        builder.AppendLine("  The tool never requests or stores module source code or private keys.");
        return builder.ToString();
    }

    public byte[] BuildZip(string promptJson, string exampleResponsesJson, string instructionsMarkdown)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, PromptEntryName, promptJson);
            WriteEntry(zip, ExampleResponsesEntryName, exampleResponsesJson);
            WriteEntry(zip, InstructionsEntryName, instructionsMarkdown);
        }

        return buffer.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        // No BOM: strict JSON parsers (e.g. Python's json module) reject BOM-prefixed files.
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
