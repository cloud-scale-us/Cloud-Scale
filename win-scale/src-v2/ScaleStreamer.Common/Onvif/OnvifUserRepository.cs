using SharpOnvifServer;
using ScaleStreamer.Common.Settings;
using Serilog;

namespace ScaleStreamer.Common.Onvif;

public class OnvifUserRepository : IUserRepository
{
    private static readonly ILogger _log = Log.ForContext<OnvifUserRepository>();

    public Task<UserInfo> GetUser(string userName)
    {
        var settings = AppSettings.Instance;
        _log.Information("ONVIF auth: GetUser called for username={UserName}", userName);

        if (string.Equals(userName, settings.RtspStream.Username, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new UserInfo(settings.RtspStream.Username, settings.RtspStream.Password));
        }

        _log.Warning("ONVIF auth: unknown username '{UserName}' (expected '{Expected}')", userName, settings.RtspStream.Username);
        return Task.FromResult<UserInfo>(null!);
    }
}
