using advent.Data.Rail;
using advent.Data.Weather;
using Microsoft.Extensions.DependencyInjection;

namespace advent;

internal static class AdventServiceCollectionExtensions
{
    public static IServiceCollection AddAdventApplication(this IServiceCollection services, string[] args)
    {
        var hostOptions = AdventHostOptions.FromArgs(args);
        var matrixOutputOptions = MatrixOutputOptions.FromEnvironmentAndArgs(
            args,
            hostOptions.MatrixWidth,
            hostOptions.MatrixHeight);
        var webControlOptions = WebControlOptions.FromEnvironment();
        var weatherOptions = WeatherOptions.FromEnvironment();
        var railOptions = RailBoardOptions.TryFromEnvironment();

        services.AddSingleton(hostOptions);
        services.AddSingleton(matrixOutputOptions);
        services.AddSingleton(webControlOptions);
        services.AddSingleton(weatherOptions);

        services.AddSingleton<WeatherSnapshotStore>();
        services.AddSingleton<IWeatherSnapshotSource>(sp => sp.GetRequiredService<WeatherSnapshotStore>());
        services.AddHostedService<BackgroundRefreshHostedService<WeatherSnapshotStore>>();

        if (railOptions is not null)
        {
            services.AddSingleton(railOptions);
            services.AddSingleton<RailSnapshotStore>(static sp =>
                new RailSnapshotStore(sp.GetRequiredService<RailBoardOptions>()));
            services.AddSingleton<IRailSnapshotSource>(sp => sp.GetRequiredService<RailSnapshotStore>());
            services.AddHostedService<BackgroundRefreshHostedService<RailSnapshotStore>>();
        }
        else
        {
            services.AddSingleton<IRailSnapshotSource>(NullRailSnapshotSource.Instance);
        }

        services.AddSingleton(sp => new SceneModuleContext(
            hostOptions.Month,
            hostOptions.ImageSceneDirectory,
            ExtraImageSceneDirectories: null,
            sp.GetRequiredService<IWeatherSnapshotSource>(),
            sp.GetRequiredService<IRailSnapshotSource>(),
            RailConfigured: railOptions is not null));

        services.AddSingleton<ISceneModule, WeatherSceneModule>();
        services.AddSingleton<ISceneModule, BuiltinSceneModule>();
        services.AddSingleton<ISceneModule, RailSceneModule>();
        services.AddSingleton<ISceneModule, SeasonalSceneModule>();
        services.AddSingleton<ISceneModule, ImageSceneModule>();
        services.AddSingleton<ISceneModule, ManualSceneModule>();

        services.AddSingleton<ISceneCatalog>(sp =>
            SceneCatalog.Create(sp.GetServices<ISceneModule>(), sp.GetRequiredService<SceneModuleContext>()));
        services.AddSingleton<ISceneScheduler>(sp => new SceneSelector(sp.GetRequiredService<ISceneCatalog>()));
        services.AddSingleton<ScenePlaybackEngine>();
        services.AddSingleton<SceneControlService>(sp => new SceneControlService(
            sp.GetRequiredService<ScenePlaybackEngine>(),
            sp.GetRequiredService<ISceneScheduler>(),
            hostOptions.IsTestMode));
        services.AddSingleton<SceneRenderer>();
        services.AddSingleton<IMatrixOutput>(sp => MatrixOutputFactory.Create(
            sp.GetRequiredService<MatrixOutputOptions>(),
            hostOptions.MatrixWidth,
            hostOptions.MatrixHeight));

        services.AddHostedService<RenderLoopHostedService>();
        services.AddHostedService<WebControlHostedService>();

        return services;
    }
}
