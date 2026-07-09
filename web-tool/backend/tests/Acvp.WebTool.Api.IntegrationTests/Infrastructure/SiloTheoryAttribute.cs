using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests.Infrastructure;

/// <summary>Theory variant of <see cref="SiloFactAttribute"/> for golden-parity matrices.</summary>
public sealed class SiloTheoryAttribute : TheoryAttribute
{
    public SiloTheoryAttribute()
    {
        Skip = new SiloFactAttribute().Skip;
    }
}
