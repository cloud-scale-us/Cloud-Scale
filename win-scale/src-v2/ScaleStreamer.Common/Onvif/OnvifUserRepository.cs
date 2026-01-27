using SharpOnvifServer;
using ScaleStreamer.Common.Settings;

namespace ScaleStreamer.Common.Onvif;

public class OnvifUserRepository : IUserRepository
{
    public Task<UserInfo> GetUser(string userName)
    {
        var settings = AppSettings.Instance;
        if (string.Equals(userName, settings.RtspStream.Username, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new UserInfo(settings.RtspStream.Username, settings.RtspStream.Password));
        }
        return Task.FromResult<UserInfo>(null!);
    }
}
