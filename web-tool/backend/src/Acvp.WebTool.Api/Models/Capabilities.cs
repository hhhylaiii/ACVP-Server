namespace Acvp.WebTool.Api.Models;

/// <summary>One supported algorithm with its modes and parameter sets.</summary>
public sealed record AlgorithmCapability(string Algorithm, string[] Modes, string[] ParameterSets);

/// <summary>The full supported selection matrix returned by GET /api/capabilities.</summary>
public sealed record Capabilities(AlgorithmCapability[] Algorithms);

/// <summary>Result of a synchronous parameter check (POST /api/check).</summary>
public sealed record CheckResult(bool Valid, string[] Messages);
