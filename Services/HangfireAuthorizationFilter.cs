using Hangfire.Dashboard;

namespace ZeniSearch.Api.Services;

//Simple authorization for Hangfire Dashboard
//Add authenication if using for production
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Development: Allow all
        // Production: Check user roles/claims

        var httpContext = context.GetHttpContext();

        return true; //change this if in production
    }
}