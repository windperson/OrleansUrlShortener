using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;

namespace UrlShortener.Infra.Silo
{
    public static class AzureAppInsightExtensions
    {
        public class CloudRoleNameTelemetryInitializer : ITelemetryInitializer
        {
            private readonly string _roleName;
            public CloudRoleNameTelemetryInitializer(string roleName)
            {
                _roleName = roleName;
            }

            public void Initialize(ITelemetry telemetry)
            {
                // set custom role name here
                telemetry.Context.Cloud.RoleName = _roleName;
            }
        }

        public static IServiceCollection SetAzureAppInsightRoleName(this IServiceCollection services, string roleName)
        {
            services.AddSingleton<ITelemetryInitializer>( new CloudRoleNameTelemetryInitializer(roleName));
            return services;
        }
    }
}
