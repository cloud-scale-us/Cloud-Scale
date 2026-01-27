using CoreWCF;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Logging;
using ScaleStreamer.Common.Settings;
using SharpOnvifServer.Media;

namespace ScaleStreamer.Common.Onvif;

public class ScaleMediaService : MediaBase
{
    private const string PROFILE_TOKEN = "ScaleStream";
    private const string VIDEO_SOURCE_TOKEN = "VideoSource_1";
    private const string VIDEO_ENCODER_TOKEN = "VideoEncoder_1";

    private readonly IServer _server;
    private readonly ILogger<ScaleMediaService> _logger;

    public ScaleMediaService(IServer server, ILogger<ScaleMediaService> logger)
    {
        _server = server;
        _logger = logger;
    }

    private static RtspStreamSettings Rtsp => AppSettings.Instance.RtspStream;

    private string GetRtspUri()
    {
        var settings = AppSettings.Instance;
        var nic = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(i => i.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                                 i.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel &&
                                 i.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up);
        var ip = "127.0.0.1";
        if (nic != null)
        {
            var addr = nic.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (addr != null) ip = addr.Address.ToString();
        }

        if (settings.RtspStream.RequireAuth)
            return $"rtsp://{settings.RtspStream.Username}:{settings.RtspStream.Password}@{ip}:{settings.RtspStream.Port}/scale";
        return $"rtsp://{ip}:{settings.RtspStream.Port}/scale";
    }

    public override GetProfilesResponse GetProfiles(GetProfilesRequest request)
    {
        return new GetProfilesResponse
        {
            Profiles = new[] { CreateProfile() }
        };
    }

    [return: MessageParameter(Name = "Profile")]
    public override Profile GetProfile(string ProfileToken)
    {
        if (ProfileToken != PROFILE_TOKEN)
            SharpOnvifServer.OnvifErrors.ReturnSenderInvalidArg();
        return CreateProfile();
    }

    public override MediaUri GetStreamUri(StreamSetup StreamSetup, string ProfileToken)
    {
        if (ProfileToken != PROFILE_TOKEN)
            SharpOnvifServer.OnvifErrors.ReturnSenderInvalidArg();

        return new MediaUri { Uri = GetRtspUri() };
    }

    public override MediaUri GetSnapshotUri(string ProfileToken)
    {
        return new MediaUri { Uri = "" };
    }

    public override GetVideoSourcesResponse GetVideoSources(GetVideoSourcesRequest request)
    {
        return new GetVideoSourcesResponse
        {
            VideoSources = new[]
            {
                new VideoSource
                {
                    token = VIDEO_SOURCE_TOKEN,
                    Resolution = new VideoResolution
                    {
                        Width = Rtsp.VideoWidth,
                        Height = Rtsp.VideoHeight,
                    },
                    Framerate = Rtsp.FrameRate,
                    Imaging = new ImagingSettings { Brightness = 100 },
                }
            }
        };
    }

    [return: MessageParameter(Name = "Configuration")]
    public override VideoSourceConfiguration GetVideoSourceConfiguration(string ConfigurationToken)
    {
        return GetVideoSourceConfig();
    }

    [return: MessageParameter(Name = "Configurations")]
    public override GetVideoSourceConfigurationsResponse GetVideoSourceConfigurations(GetVideoSourceConfigurationsRequest request)
    {
        return new GetVideoSourceConfigurationsResponse
        {
            Configurations = new[] { GetVideoSourceConfig() }
        };
    }

    [return: MessageParameter(Name = "Configuration")]
    public override VideoEncoderConfiguration GetVideoEncoderConfiguration(string ConfigurationToken)
    {
        return GetVideoEncoderConfig();
    }

    [return: MessageParameter(Name = "Configurations")]
    public override GetVideoEncoderConfigurationsResponse GetVideoEncoderConfigurations(GetVideoEncoderConfigurationsRequest request)
    {
        return new GetVideoEncoderConfigurationsResponse
        {
            Configurations = new[] { GetVideoEncoderConfig() }
        };
    }

    [return: MessageParameter(Name = "Options")]
    public override VideoEncoderConfigurationOptions GetVideoEncoderConfigurationOptions(string ConfigurationToken, string ProfileToken)
    {
        return new VideoEncoderConfigurationOptions
        {
            JPEG = new JpegOptions
            {
                EncodingIntervalRange = new IntRange { Min = 1, Max = 100 },
                FrameRateRange = new IntRange { Min = 1, Max = 30 },
                ResolutionsAvailable = new[]
                {
                    new VideoResolution { Width = Rtsp.VideoWidth, Height = Rtsp.VideoHeight }
                }
            },
            QualityRange = new IntRange { Min = 1, Max = 100 },
        };
    }

    [return: MessageParameter(Name = "Options")]
    public override VideoSourceConfigurationOptions GetVideoSourceConfigurationOptions(string ConfigurationToken, string ProfileToken)
    {
        return new VideoSourceConfigurationOptions
        {
            VideoSourceTokensAvailable = new[] { VIDEO_SOURCE_TOKEN },
            BoundsRange = new IntRectangleRange
            {
                XRange = new IntRange { Min = 0, Max = Rtsp.VideoWidth },
                YRange = new IntRange { Min = 0, Max = Rtsp.VideoHeight },
                WidthRange = new IntRange { Min = 0, Max = Rtsp.VideoWidth },
                HeightRange = new IntRange { Min = 0, Max = Rtsp.VideoHeight },
            },
            MaximumNumberOfProfiles = 1,
            MaximumNumberOfProfilesSpecified = true,
        };
    }

    [return: MessageParameter(Name = "Configurations")]
    public override GetCompatibleVideoEncoderConfigurationsResponse GetCompatibleVideoEncoderConfigurations(GetCompatibleVideoEncoderConfigurationsRequest request)
    {
        return new GetCompatibleVideoEncoderConfigurationsResponse
        {
            Configurations = new[] { GetVideoEncoderConfig() }
        };
    }

    public override GetAudioSourcesResponse GetAudioSources(GetAudioSourcesRequest request)
    {
        return new GetAudioSourcesResponse { AudioSources = Array.Empty<AudioSource>() };
    }

    [return: MessageParameter(Name = "Configurations")]
    public override GetAudioEncoderConfigurationsResponse GetAudioEncoderConfigurations(GetAudioEncoderConfigurationsRequest request)
    {
        return new GetAudioEncoderConfigurationsResponse { Configurations = Array.Empty<AudioEncoderConfiguration>() };
    }

    [return: MessageParameter(Name = "Configurations")]
    public override GetAudioSourceConfigurationsResponse GetAudioSourceConfigurations(GetAudioSourceConfigurationsRequest request)
    {
        return new GetAudioSourceConfigurationsResponse { Configurations = Array.Empty<AudioSourceConfiguration>() };
    }

    [return: MessageParameter(Name = "Configurations")]
    public override GetAudioOutputConfigurationsResponse GetAudioOutputConfigurations(GetAudioOutputConfigurationsRequest request)
    {
        return new GetAudioOutputConfigurationsResponse { Configurations = Array.Empty<AudioOutputConfiguration>() };
    }

    [return: MessageParameter(Name = "Configurations")]
    public override GetCompatibleAudioDecoderConfigurationsResponse GetCompatibleAudioDecoderConfigurations(GetCompatibleAudioDecoderConfigurationsRequest request)
    {
        return new GetCompatibleAudioDecoderConfigurationsResponse { Configurations = Array.Empty<AudioDecoderConfiguration>() };
    }

    [return: MessageParameter(Name = "Options")]
    public override AudioEncoderConfigurationOptions GetAudioEncoderConfigurationOptions(string ConfigurationToken, string ProfileToken)
    {
        return new AudioEncoderConfigurationOptions { Options = Array.Empty<AudioEncoderConfigurationOption>() };
    }

    public override void SetVideoEncoderConfiguration(VideoEncoderConfiguration Configuration, bool ForcePersistence)
    {
        _logger.LogInformation("ONVIF Media: SetVideoEncoderConfiguration (no-op)");
    }

    public override void AddPTZConfiguration(string ProfileToken, string ConfigurationToken)
    {
        _logger.LogInformation("ONVIF Media: AddPTZConfiguration (no-op)");
    }

    public override void AddAudioOutputConfiguration(string ProfileToken, string ConfigurationToken)
    {
        _logger.LogInformation("ONVIF Media: AddAudioOutputConfiguration (no-op)");
    }

    public override void AddAudioDecoderConfiguration(string ProfileToken, string ConfigurationToken)
    {
        _logger.LogInformation("ONVIF Media: AddAudioDecoderConfiguration (no-op)");
    }

    #region Private Helpers

    private Profile CreateProfile()
    {
        return new Profile
        {
            token = PROFILE_TOKEN,
            Name = "Weight Display Stream",
            VideoSourceConfiguration = GetVideoSourceConfig(),
            VideoEncoderConfiguration = GetVideoEncoderConfig(),
        };
    }

    private static VideoSourceConfiguration GetVideoSourceConfig()
    {
        return new VideoSourceConfiguration
        {
            token = VIDEO_SOURCE_TOKEN,
            Name = VIDEO_SOURCE_TOKEN,
            SourceToken = VIDEO_SOURCE_TOKEN,
            Bounds = new IntRectangle
            {
                x = 0, y = 0,
                width = Rtsp.VideoWidth,
                height = Rtsp.VideoHeight,
            },
            UseCount = 1,
        };
    }

    private static VideoEncoderConfiguration GetVideoEncoderConfig()
    {
        return new VideoEncoderConfiguration
        {
            token = VIDEO_ENCODER_TOKEN,
            Name = VIDEO_ENCODER_TOKEN,
            UseCount = 1,
            Encoding = VideoEncoding.JPEG,
            Resolution = new VideoResolution
            {
                Width = Rtsp.VideoWidth,
                Height = Rtsp.VideoHeight,
            },
            Quality = 100.0f,
            RateControl = new VideoRateControl
            {
                FrameRateLimit = Rtsp.FrameRate,
                BitrateLimit = 8192,
                EncodingInterval = 1,
            },
        };
    }

    #endregion
}
