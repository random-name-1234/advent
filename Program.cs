using Microsoft.Extensions.Hosting;

namespace advent;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddAdventApplication(args);

        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
