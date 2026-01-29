using CoreWCF;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Logging;
using ScaleStreamer.Common.Settings;
using SharpOnvifCommon;
using SharpOnvifServer.DeviceMgmt;
using NetNic = System.Net.NetworkInformation.NetworkInterface;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ScaleStreamer.Common.Onvif;

public class ScaleDeviceService : DeviceBase
{
    private readonly IServer _server;
    private readonly ILogger<ScaleDeviceService> _logger;

    private readonly string _primaryIPv4;
    private readonly string _primaryMac;
    private readonly string _primaryNicName;
    private readonly string _primaryGateway;
    private readonly string _primaryDns;

    public ScaleDeviceService(IServer server, ILogger<ScaleDeviceService> logger)
    {
        _server = server;
        _logger = logger;

        var nic = GetPrimaryNic();
        _primaryIPv4 = GetIPv4(nic);
        _primaryMac = GetMac(nic);
        _primaryNicName = nic?.Name ?? "eth0";
        _primaryGateway = GetGateway(nic);
        _primaryDns = GetDns(nic);
    }

    public override GetDeviceInformationResponse GetDeviceInformation(GetDeviceInformationRequest request)
    {
        return new GetDeviceInformationResponse
        {
            FirmwareVersion = "9.80.3.1",
            HardwareId = "7D2",
            Manufacturer = "AXIS",
            Model = "AXIS M1065-LW",
            SerialNumber = AppSettings.Instance.ScaleConnection.ScaleId,
        };
    }

    public override GetCapabilitiesResponse GetCapabilities(GetCapabilitiesRequest request)
    {
        var endpointUri = OperationContext.Current.IncomingMessageProperties.Via;

        return new GetCapabilitiesResponse
        {
            Capabilities = new Capabilities
            {
                Device = new DeviceCapabilities
                {
                    XAddr = OnvifHelpers.ChangeUriPath(endpointUri, "/onvif/device_service").ToString(),
                    Network = new NetworkCapabilities1
                    {
                        IPFilter = false,
                        ZeroConfiguration = false,
                        IPVersion6 = false,
                        DynDNS = false,
                    },
                    System = new SystemCapabilities1
                    {
                        SystemLogging = false,
                        SupportedVersions = new[]
                        {
                            new OnvifVersion { Major = 16, Minor = 12 }
                        },
                    },
                    IO = new IOCapabilities
                    {
                        InputConnectors = 0,
                        RelayOutputs = 0,
                        RelayOutputsSpecified = true,
                        InputConnectorsSpecified = true,
                    },
                    Security = new SecurityCapabilities1
                    {
                        TLS12 = false,
                    },
                },
                Media = new MediaCapabilities
                {
                    XAddr = OnvifHelpers.ChangeUriPath(endpointUri, "/onvif/media_service").ToString(),
                    StreamingCapabilities = new RealTimeStreamingCapabilities
                    {
                        RTP_RTSP_TCP = true,
                        RTP_RTSP_TCPSpecified = true,
                    },
                },
            }
        };
    }

    public override GetScopesResponse GetScopes(GetScopesRequest request)
    {
        return new GetScopesResponse
        {
            Scopes = new[]
            {
                new Scope
                {
                    ScopeDef = ScopeDefinition.Fixed,
                    ScopeItem = "onvif://www.onvif.org/type/video_encoder"
                },
                new Scope
                {
                    ScopeDef = ScopeDefinition.Fixed,
                    ScopeItem = "onvif://www.onvif.org/type/Network_Video_Transmitter"
                },
                new Scope
                {
                    ScopeDef = ScopeDefinition.Fixed,
                    ScopeItem = "onvif://www.onvif.org/Profile/Streaming"
                },
                new Scope
                {
                    ScopeDef = ScopeDefinition.Fixed,
                    ScopeItem = "onvif://www.onvif.org/name/AXIS%20M1065-LW"
                },
                new Scope
                {
                    ScopeDef = ScopeDefinition.Fixed,
                    ScopeItem = "onvif://www.onvif.org/hardware/AXIS%20M1065-LW"
                },
            }
        };
    }

    public override GetServicesResponse GetServices(GetServicesRequest request)
    {
        var endpointUri = OperationContext.Current.IncomingMessageProperties.Via;

        return new GetServicesResponse
        {
            Service = new[]
            {
                new Service
                {
                    Namespace = OnvifServices.DEVICE_MGMT,
                    XAddr = OnvifHelpers.ChangeUriPath(endpointUri, "/onvif/device_service").ToString(),
                    Version = new OnvifVersion { Major = 17, Minor = 12 },
                },
                new Service
                {
                    Namespace = OnvifServices.MEDIA,
                    XAddr = OnvifHelpers.ChangeUriPath(endpointUri, "/onvif/media_service").ToString(),
                    Version = new OnvifVersion { Major = 17, Minor = 12 },
                },
            }
        };
    }

    public override GetNetworkInterfacesResponse GetNetworkInterfaces(GetNetworkInterfacesRequest request)
    {
        return new GetNetworkInterfacesResponse
        {
            NetworkInterfaces = new[]
            {
                new SharpOnvifServer.DeviceMgmt.NetworkInterface
                {
                    Enabled = true,
                    Info = new NetworkInterfaceInfo
                    {
                        Name = _primaryNicName,
                        HwAddress = _primaryMac,
                    },
                    Link = new NetworkInterfaceLink
                    {
                        AdminSettings = new NetworkInterfaceConnectionSetting(),
                        OperSettings = new NetworkInterfaceConnectionSetting(),
                    },
                    IPv4 = new IPv4NetworkInterface
                    {
                        Config = new IPv4Configuration
                        {
                            Manual = new[]
                            {
                                new PrefixedIPv4Address
                                {
                                    Address = _primaryIPv4,
                                    PrefixLength = 24,
                                }
                            },
                        },
                        Enabled = true,
                    },
                },
            }
        };
    }

    public override SystemDateTime GetSystemDateAndTime()
    {
        var now = System.DateTime.UtcNow;
        return new SystemDateTime
        {
            UTCDateTime = new SharpOnvifServer.DeviceMgmt.DateTime
            {
                Date = new Date { Day = now.Day, Month = now.Month, Year = now.Year },
                Time = new Time { Hour = now.Hour, Minute = now.Minute, Second = now.Second },
            }
        };
    }

    [return: MessageParameter(Name = "HostnameInformation")]
    public override HostnameInformation GetHostname()
    {
        return new HostnameInformation { Name = Environment.MachineName };
    }

    [return: MessageParameter(Name = "DiscoveryMode")]
    public override DiscoveryMode GetDiscoveryMode()
    {
        return DiscoveryMode.Discoverable;
    }

    [return: MessageParameter(Name = "NetworkProtocols")]
    public override GetNetworkProtocolsResponse GetNetworkProtocols(GetNetworkProtocolsRequest request)
    {
        var settings = AppSettings.Instance;
        return new GetNetworkProtocolsResponse
        {
            NetworkProtocols = new[]
            {
                new NetworkProtocol
                {
                    Enabled = true,
                    Name = NetworkProtocolType.RTSP,
                    Port = new[] { settings.RtspStream.Port },
                }
            }
        };
    }

    public override DNSInformation GetDNS()
    {
        return new DNSInformation
        {
            DNSManual = new[] { new IPAddress { IPv4Address = _primaryDns } }
        };
    }

    [return: MessageParameter(Name = "NetworkGateway")]
    public override NetworkGateway GetNetworkDefaultGateway()
    {
        return new NetworkGateway
        {
            IPv4Address = new[] { _primaryGateway }
        };
    }

    [return: MessageParameter(Name = "Capabilities")]
    public override DeviceServiceCapabilities GetServiceCapabilities()
    {
        return new DeviceServiceCapabilities();
    }

    [return: MessageParameter(Name = "User")]
    public override GetUsersResponse GetUsers(GetUsersRequest request)
    {
        var settings = AppSettings.Instance;
        return new GetUsersResponse
        {
            User = new[]
            {
                new User
                {
                    Username = settings.RtspStream.Username,
                    UserLevel = UserLevel.Administrator,
                }
            }
        };
    }

    public override void SetSystemFactoryDefault(FactoryDefaultType FactoryDefault)
    {
        _logger.LogInformation("ONVIF: SetSystemFactoryDefault (no-op)");
    }

    [return: MessageParameter(Name = "Message")]
    public override string SystemReboot()
    {
        _logger.LogInformation("ONVIF: SystemReboot (no-op)");
        return "Reboot not supported";
    }

    [return: MessageParameter(Name = "SystemLog")]
    public override SystemLog GetSystemLog(SystemLogType LogType)
    {
        return new SystemLog { String = "Scale Streamer ONVIF Service" };
    }

    public override void SetSystemDateAndTime(SetDateTimeType DateTimeType, bool DaylightSavings,
        SharpOnvifServer.DeviceMgmt.TimeZone TimeZone, SharpOnvifServer.DeviceMgmt.DateTime UTCDateTime)
    {
        _logger.LogInformation("ONVIF: SetSystemDateAndTime (no-op)");
    }

    #region Network Helpers

    private static NetNic? GetPrimaryNic()
    {
        return NetNic.GetAllNetworkInterfaces().FirstOrDefault(
            i => i.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                 i.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                 i.OperationalStatus == OperationalStatus.Up);
    }

    private static string GetIPv4(NetNic? nic)
    {
        if (nic == null) return "127.0.0.1";
        var addr = nic.GetIPProperties().UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        return addr?.Address.ToString() ?? "127.0.0.1";
    }

    private static string GetMac(NetNic? nic)
    {
        if (nic == null) return "00:00:00:00:00:00";
        return BitConverter.ToString(nic.GetPhysicalAddress().GetAddressBytes()).Replace('-', ':');
    }

    private static string GetGateway(NetNic? nic)
    {
        if (nic == null) return "127.0.0.1";
        var gw = nic.GetIPProperties().GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
        return gw?.Address.ToString() ?? "127.0.0.1";
    }

    private static string GetDns(NetNic? nic)
    {
        if (nic == null) return "127.0.0.1";
        var dns = nic.GetIPProperties().DnsAddresses
            .FirstOrDefault(d => d.AddressFamily == AddressFamily.InterNetwork);
        return dns?.ToString() ?? "127.0.0.1";
    }

    #endregion
}
