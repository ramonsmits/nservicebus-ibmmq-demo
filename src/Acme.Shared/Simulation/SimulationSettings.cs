using System.Text.Json;

namespace Acme;

public static class SimulationSettings
{
    static int _failurePercentage;
    static int _failureInterval;
    static int _failureCounter;

    static readonly string ConfigFile = Path.Combine(
        Environment.GetEnvironmentVariable("CONCURRENCY_CONFIG_DIR") ?? Path.GetTempPath(),
        "simulation-settings.json");

    static SimulationSettings()
    {
        var persisted = Read();
        if (persisted != null) Apply(persisted);
    }

    public static int FailurePercentage
    {
        get => _failurePercentage;
        set
        {
            _failurePercentage = value;
            _failureInterval = value > 0 ? 100 / value : 0;
        }
    }

    public static int FailureInterval => _failureInterval;

    public static bool ShouldFail() =>
        _failureInterval > 0 && Interlocked.Increment(ref _failureCounter) % _failureInterval == 0;

    public static int ProcessingDelayMs { get; set; }
    public static int Concurrency { get; set; } = Math.Max(2, Environment.ProcessorCount);
    public static string TransactionMode { get; set; } = "SendsAtomicWithReceive";

    public static NServiceBus.TransportTransactionMode TransportTransactionMode => TransactionMode switch
    {
        "ReceiveOnly" => NServiceBus.TransportTransactionMode.ReceiveOnly,
        "None" => NServiceBus.TransportTransactionMode.None,
        _ => NServiceBus.TransportTransactionMode.SendsAtomicWithReceive
    };

    public static void Apply(UpdateSimulationSettings settings)
    {
        FailurePercentage = settings.FailurePercentage;
        ProcessingDelayMs = settings.ProcessingDelayMs;
        if (settings.Concurrency > 0)
            Concurrency = settings.Concurrency;
        if (!string.IsNullOrEmpty(settings.TransactionMode))
            TransactionMode = settings.TransactionMode;
    }

    public static void Save(UpdateSimulationSettings settings)
    {
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(settings));
    }

    static UpdateSimulationSettings? Read()
    {
        if (!File.Exists(ConfigFile)) return null;
        try { return JsonSerializer.Deserialize<UpdateSimulationSettings>(File.ReadAllText(ConfigFile)); }
        catch { return null; }
    }
}
