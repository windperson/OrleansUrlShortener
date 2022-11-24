using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace UrlShortener.Infra.Silo;

public static class ConfigurationOptionsExtension
{
    public static T GetOptions<T>(this HostBuilderContext hostBuilderContext, string sectionName) where T : new()
    {
        var options = new T();
        hostBuilderContext.Configuration.GetSection(sectionName).Bind(options);
        return options;
    }
}