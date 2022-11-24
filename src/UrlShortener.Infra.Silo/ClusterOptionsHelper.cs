using Orleans.Configuration;

namespace UrlShortener.Infra.Silo;

public static class ClusterOptionsHelper
{
    private const string WindowsDeploymentSlotEnv = "WEBSITE_DEPLOYMENT_ID";
    private const string LinuxDeploymentSlotEnv = "WEBSITE_SITE_NAME";
    public static ClusterOptions CreateClusterOptions(string clusterIdPrefix, string serviceId)
    {
        string clusterId;
        // azure web app (Windows) has a "WEBSITE_DEPLOYMENT_ID" environment variable that is different on multiple deployment slots
        if (OperatingSystem.IsWindows() &&
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(WindowsDeploymentSlotEnv)))
        {
            var websiteDeploymentId = Environment.GetEnvironmentVariable(WindowsDeploymentSlotEnv);
            clusterId = $"{clusterIdPrefix}{websiteDeploymentId}";
        }
        // azure web app (Linux) has a "WEBSITE_SITE_NAME" environment variable that is different on multiple deployment slots
        else if (OperatingSystem.IsLinux() &&
                 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LinuxDeploymentSlotEnv)))
        {
            var slotName = Environment.GetEnvironmentVariable(LinuxDeploymentSlotEnv);
            clusterId = $"{clusterIdPrefix}{slotName}";
        }
        else
        {
            clusterId = $"{clusterIdPrefix}single-slot";
        }

        return new ClusterOptions { ClusterId = clusterId, ServiceId = serviceId };
    }
}