using Hangfire.Dashboard;

namespace AssetMgmt.Infrastructure.Jobs;

/// <summary>
/// Authorization for the Hangfire dashboard. The dashboard is browser-navigated
/// and cannot present our JWT. It is therefore disabled by default and, when
/// explicitly enabled, is reachable only from the local machine.
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        return remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp);
    }
}
