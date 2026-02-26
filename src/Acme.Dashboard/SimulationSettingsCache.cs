using System.Collections.Concurrent;

namespace Acme;

public sealed class SimulationSettingsCache
{
    readonly ConcurrentDictionary<string, SimulationSettingsResponse> _cache = new();

    public SimulationSettingsResponse? Get(string endpoint) =>
        _cache.GetValueOrDefault(endpoint);

    public void Update(SimulationSettingsResponse response) =>
        _cache[response.Endpoint] = response;
}

public sealed class SimulationSettingsReportHandler(SimulationSettingsCache cache) : IHandleMessages<SimulationSettingsResponse>
{
    public Task Handle(SimulationSettingsResponse message, IMessageHandlerContext context)
    {
        cache.Update(message);
        return Task.CompletedTask;
    }
}

public sealed class SimulationSettingsStartup(IMessageSession session) : IHostedService
{
    static readonly string[] Endpoints = ["Acme.Dashboard", "Acme.Sales", "Acme.Billing", "Acme.Shipping"];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var endpoint in Endpoints)
        {
            var options = new SendOptions();
            options.SetDestination(endpoint + ".control");
            await session.Send(new GetSimulationSettings(), options);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
