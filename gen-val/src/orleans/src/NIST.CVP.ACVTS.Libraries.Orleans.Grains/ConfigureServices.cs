using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NIST.CVP.ACVTS.Libraries.Common;
using NIST.CVP.ACVTS.Libraries.Common.Config;
using NIST.CVP.ACVTS.Libraries.Common.Interfaces;
using NIST.CVP.ACVTS.Libraries.Common.Services;
using NIST.CVP.ACVTS.Libraries.Crypto.SHA.NativeFastSha;
using NIST.CVP.ACVTS.Libraries.Math;
using NIST.CVP.ACVTS.Libraries.Math.Entropy;
using IShaFactory = NIST.CVP.ACVTS.Libraries.Crypto.Common.Hash.ShaWrapper.IShaFactory;

namespace NIST.CVP.ACVTS.Libraries.Orleans.Grains
{
    /// <summary>
    /// Performs service injection for orleans.
    /// Trimmed to the FIPS 203 (ML-KEM) / FIPS 204 (ML-DSA) scope: the PQC grains
    /// construct their Kyber/Dilithium primitives directly and only rely on the
    /// scheduler, entropy, random and SHA factory registrations below.
    /// </summary>
    public static class ConfigureServices
    {
        public static void RegisterServices(IConfiguration configuration, IServiceCollection svc)
        {
            svc.AddSingleton(configuration);
            svc.AddSingleton<IDbConnectionStringFactory, DbConnectionStringFactory>();
            svc.AddSingleton<IDbConnectionFactory, SqlDbConnectionFactory>();

            svc.Configure<EnvironmentConfig>(configuration.GetSection(nameof(EnvironmentConfig)));
            svc.Configure<PoolConfig>(configuration.GetSection(nameof(PoolConfig)));
            svc.Configure<OrleansConfig>(configuration.GetSection(nameof(OrleansConfig)));

            var serviceProvider = svc.BuildServiceProvider();
            var orleansConfig = serviceProvider.GetService<IOptions<OrleansConfig>>().Value;
            RegisterServices(svc, orleansConfig);
        }

        private static void RegisterServices(IServiceCollection svc, OrleansConfig orleansConfig)
        {
            svc.AddSingleton(new LimitedConcurrencyLevelTaskScheduler(GetOrleansNodeMaxConcurrency(orleansConfig)));
            svc.AddSingleton<IEntropyProviderFactory, EntropyProviderFactory>();
            svc.AddSingleton<IRandom800_90, Random800_90>();
            svc.AddSingleton<IEntropyProvider, EntropyProvider>();

            svc.AddSingleton<IShaFactory, NativeShaFactory>();
        }

        private static int GetOrleansNodeMaxConcurrency(OrleansConfig orleansConfig)
        {
            var localIpAddress = GetLocalIpAddress();

            var nodeConfig = orleansConfig.OrleansNodeConfig
                .FirstOrDefault(f => f.HostName.Equals(localIpAddress, StringComparison.OrdinalIgnoreCase) ||
                                     f.HostName.Equals("localhost", StringComparison.OrdinalIgnoreCase));

            if (nodeConfig == null)
            {
                throw new Exception("Could not reconcile IP address of node. Ensure this node's IP address is listed within appsettings.[env].json under 'OrleansNodeConfig'");
            }

            return nodeConfig.MaxConcurrentWork;
        }

        private static string GetLocalIpAddress()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString();
            }
        }
    }
}
