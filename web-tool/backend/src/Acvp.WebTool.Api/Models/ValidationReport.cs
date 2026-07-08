namespace Acvp.WebTool.Api.Models;

/// <summary>Per-test-case grading outcome.</summary>
public sealed record ValidationCase(int TcId, bool Passed, string? Reason = null);

/// <summary>Counts across all graded test cases.</summary>
public sealed record ValidationSummary(int Total, int Passed, int Failed);

/// <summary>
/// The human-readable graded outcome for a validate job. The overall disposition
/// mirrors the engine's validation.json verdict exactly (FR-007 golden parity).
/// </summary>
public sealed record ValidationReport(
    string JobId,
    string Disposition,
    ValidationSummary Summary,
    ValidationCase[] Cases);
