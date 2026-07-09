using System.Net.Sockets;
using Xunit;

namespace Acvp.WebTool.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Golden-parity tests need the real Orleans silo (and optionally the CLI oracle).
/// They run only when ACVP_WEBTOOL_GOLDEN_PARITY=1 and the silo gateway answers,
/// so the default test run stays fast and hermetic.
/// </summary>
public sealed class SiloFactAttribute : FactAttribute
{
    public SiloFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ACVP_WEBTOOL_GOLDEN_PARITY") != "1")
        {
            Skip = "Set ACVP_WEBTOOL_GOLDEN_PARITY=1 (with the Orleans silo running) to run golden-parity tests.";
            return;
        }

        if (!SiloReachable())
        {
            Skip = "Orleans silo gateway (localhost:30000) is not reachable.";
        }
    }

    private static bool SiloReachable()
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("localhost", 30000).Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            return false;
        }
    }
}
