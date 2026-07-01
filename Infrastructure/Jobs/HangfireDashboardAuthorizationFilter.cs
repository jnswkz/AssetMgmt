using Hangfire.Dashboard;

namespace AssetMgmt.Infrastructure.Jobs;

/// <summary>
/// Authorization for the Hangfire dashboard. The dashboard is browser-navigated
/// and cannot present our JWT (which travels in the Authorization header), so for
/// the MVP we allow it freely in Development and restrict it to local requests
/// otherwise. Tighten this before any real production exposure.
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _isDevelopment;

    public HangfireDashboardAuthorizationFilter(bool isDevelopment)
    {
        _isDevelopment = isDevelopment;
    }

    public bool Authorize(DashboardContext context)
    {
        if (_isDevelopment)
            return true;

        var httpContext = context.GetHttpContext();
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        return remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp);
    }
}
