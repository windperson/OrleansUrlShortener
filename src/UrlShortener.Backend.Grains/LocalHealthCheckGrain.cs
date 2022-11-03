using Orleans;
using Orleans.Concurrency;

namespace UrlShortener.Backend.Grains;

[StatelessWorker(1)]
public class LocalHealthCheckGrain : Grain, ILocalHealthCheckGrain
{
    public Task PingAsync() => Task.CompletedTask;
}

public interface ILocalHealthCheckGrain : IGrainWithIntegerKey
{
    Task PingAsync();
}
