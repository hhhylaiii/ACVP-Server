using Acvp.WebTool.Api.Options;
using Acvp.WebTool.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Acvp.WebTool.Api.UnitTests;

/// <summary>
/// T007 — ArtifactStore round-trips per-job files and enforces the server-only
/// privacy boundary for internalProjection.json / expectedResults.json (FR-011, R5).
/// </summary>
public sealed class ArtifactStoreServerOnlyTests : IDisposable
{
    private readonly string _root;
    private readonly ArtifactStore _store;
    private readonly string _jobId = Guid.NewGuid().ToString();

    public ArtifactStoreServerOnlyTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"acvp-webtool-tests-{Guid.NewGuid():N}");
        _store = new ArtifactStore(Microsoft.Extensions.Options.Options.Create(new StorageOptions { ArtifactRoot = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenReadClientArtifact_RoundTripsContent()
    {
        const string content = """{"vsId":42,"testGroups":[]}""";

        await _store.SaveAsync(_jobId, ArtifactNames.Prompt, content);
        var readBack = await _store.ReadClientArtifactAsync(_jobId, ArtifactNames.Prompt);

        readBack.Should().Be(content);
        _store.Exists(_jobId, ArtifactNames.Prompt).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_KeepsJobsIsolatedByJobId()
    {
        var otherJobId = Guid.NewGuid().ToString();
        await _store.SaveAsync(_jobId, ArtifactNames.Prompt, "job-a");
        await _store.SaveAsync(otherJobId, ArtifactNames.Prompt, "job-b");

        (await _store.ReadClientArtifactAsync(_jobId, ArtifactNames.Prompt)).Should().Be("job-a");
        (await _store.ReadClientArtifactAsync(otherJobId, ArtifactNames.Prompt)).Should().Be("job-b");
    }

    [Theory]
    [InlineData(ArtifactNames.InternalProjection)]
    [InlineData(ArtifactNames.ExpectedResults)]
    public async Task ReadClientArtifact_RefusesServerOnlyArtifacts(string serverOnlyName)
    {
        await _store.SaveAsync(_jobId, serverOnlyName, "secret answer key");

        var act = () => _store.ReadClientArtifactAsync(_jobId, serverOnlyName);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "internalProjection/expectedResults must never be exposed through the client-facing read path");
    }

    [Theory]
    [InlineData(ArtifactNames.InternalProjection)]
    [InlineData(ArtifactNames.ExpectedResults)]
    public async Task ReadServerOnlyArtifact_ReadsServerOnlyArtifacts(string serverOnlyName)
    {
        await _store.SaveAsync(_jobId, serverOnlyName, "server side only");

        var readBack = await _store.ReadServerOnlyArtifactAsync(_jobId, serverOnlyName);

        readBack.Should().Be("server side only");
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("not-a-guid/..")]
    [InlineData("")]
    public async Task SaveAsync_RejectsNonGuidJobIds(string badJobId)
    {
        var act = () => _store.SaveAsync(badJobId, ArtifactNames.Prompt, "x");

        await act.Should().ThrowAsync<ArgumentException>("jobId must be a GUID to prevent path traversal");
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("arbitrary.txt")]
    public async Task SaveAsync_RejectsUnknownArtifactNames(string badName)
    {
        var act = () => _store.SaveAsync(_jobId, badName, "x");

        await act.Should().ThrowAsync<ArgumentException>("only the well-known artifact names are allowed");
    }

    [Fact]
    public async Task ReadClientArtifact_MissingArtifact_ThrowsFileNotFound()
    {
        var act = () => _store.ReadClientArtifactAsync(_jobId, ArtifactNames.Prompt);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
