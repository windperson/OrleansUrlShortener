using System.Diagnostics.CodeAnalysis;
using System.Net;

using UrlShortener.Infra.Silo.Options;

namespace UrlShortener.Infra.Silo;

public static class SiloNetworkIpPortOptionHelper
{
    public static bool HasAzureWebAppSiloNetworkIpPortOption(
        [NotNullWhen(true)] out SiloNetworkIpPortOption? siloNetworkIpPortOption)
    {
        // Azure web app will set these environment variables when it has virtual network integration configured
        // https://learn.microsoft.com/en-us/azure/app-service/reference-app-settings?tabs=kudu%2Cdotnet#networking
        var privateIpStr = Environment.GetEnvironmentVariable("WEBSITE_PRIVATE_IP");
        var privatePort = Environment.GetEnvironmentVariable("WEBSITE_PRIVATE_PORTS")?.Split(',');
        if (IPAddress.TryParse(privateIpStr, out var ipAddress) && privatePort is { Length: >= 2 }
                                                                && int.TryParse(privatePort[0], out var siloPort) &&
                                                                int.TryParse(privatePort[1], out var gatewayPort))
        {
            siloNetworkIpPortOption = new SiloNetworkIpPortOption
            {
                SiloIpAddress = ipAddress, SiloPort = siloPort, GatewayPort = gatewayPort
            };
            return true;
        }

        siloNetworkIpPortOption = null;
        return false;
    }
}