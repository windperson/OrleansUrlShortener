using System.Net;

namespace UrlShortener.Infra.Silo.Options;

public class SiloNetworkIpPortOption
{
    public SiloNetworkIpPortOption()
    {
        SiloIpAddress = IPAddress.Any;
        SiloPort = 0;
        _disableGateway = true;
        GatewayPort = 0;
    }

    public IPAddress SiloIpAddress { get; set; }

    public bool ListenOnAnyHostAddress
    {
        get;set;
    }

    private bool _disableGateway;
    public bool DisableGateway
    {
        get
        {
            return _disableGateway;
        }
        set
        {
            if (value)
            {
                GatewayPort = 0;
            }

            _disableGateway = value;
        }
    }

    public int SiloPort { get; set; }
    public int GatewayPort { get; set; }
}