using System.Reflection;

namespace Acme;

public static class StartupBanner
{
    public static void Log()
    {
        var assembly = Assembly.GetEntryAssembly()!;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var buildTime = File.GetLastWriteTimeUtc(assembly.Location);
        var age = DateTime.UtcNow - buildTime;

        Console.WriteLine($"{assembly.GetName().Name} {version} (built {buildTime:u}, {FormatAge(age)} ago)");
    }

    static string FormatAge(TimeSpan age) => age switch
    {
        { TotalMinutes: < 1 } => $"{age.Seconds}s",
        { TotalHours: < 1 } => $"{age.Minutes}m",
        { TotalDays: < 1 } => $"{(int)age.TotalHours}h {age.Minutes}m",
        _ => $"{(int)age.TotalDays}d {age.Hours}h"
    };
}
