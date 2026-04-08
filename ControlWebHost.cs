using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace advent;

internal static class ControlWebHost
{
    public static WebApplication Build(
        SceneControlService controlService,
        SceneRenderer sceneRenderer,
        MatrixFramePresenter framePresenter,
        WebControlOptions options)
    {
        var contentRoot = AppContext.BaseDirectory;
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var indexPath = Path.Combine(webRoot, "index.html");
        var hasWebUi = File.Exists(indexPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = contentRoot
        });
        builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");
        builder.Services.AddRouting();

        var app = builder.Build();

        var previewPath = Path.Combine(webRoot, "preview.html");
        var hasPreview = File.Exists(previewPath);

        if (hasWebUi)
        {
            app.MapGet("/", () => Results.File(indexPath, "text/html; charset=utf-8"));
            app.MapGet("/index.html", () => Results.File(indexPath, "text/html; charset=utf-8"));
        }

        if (hasPreview)
            app.MapGet("/preview", () => Results.File(previewPath, "text/html; charset=utf-8"));

        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            app.Use(async (context, next) =>
            {
                if (!context.Request.Path.StartsWithSegments("/api"))
                {
                    await next();
                    return;
                }

                var suppliedToken = context.Request.Headers["X-Advent-Token"].ToString();
                if (string.Equals(suppliedToken, options.Token, StringComparison.Ordinal))
                {
                    await next();
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid token." });
            });
        }

        app.MapGet("/api/scenes", () => Results.Ok(new
        {
            items = controlService.GetSceneCatalog().Items,
            available = controlService.AvailableSceneNames,
            all = controlService.AllSceneNames,
            known = controlService.KnownSceneNames
        }));

        app.MapGet("/api/status", () => Results.Ok(controlService.GetStatus()));

        app.MapGet("/api/frame/meta", () => Results.Ok(new
        {
            logicalWidth = framePresenter.LogicalWidth,
            logicalHeight = framePresenter.LogicalHeight,
            physicalWidth = framePresenter.PhysicalWidth,
            physicalHeight = framePresenter.PhysicalHeight,
            horizontalScale = framePresenter.HorizontalScale,
            verticalScale = framePresenter.VerticalScale
        }));

        app.MapPost("/api/scene/play", (PlaySceneRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Scene name is required." });

            if (!controlService.EnqueueSceneByName(request.Name, out var error))
                return Results.NotFound(new { error });

            return Results.Ok(new { queued = request.Name });
        });

        app.MapPost("/api/scene/next", () =>
        {
            controlService.EnqueueNextScene();
            return Results.Ok(new { queued = "next-random-or-cycle" });
        });

        app.MapPost("/api/message/show", (ShowMessageRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return Results.BadRequest(new { error = "Message text is required." });

            TimeSpan? sceneDuration = null;
            if (request.DurationSeconds is { } durationSeconds)
            {
                if (durationSeconds <= 0 || durationSeconds > SceneTiming.MaxSceneDuration.TotalSeconds)
                    return Results.BadRequest(new
                    {
                        error =
                            $"DurationSeconds must be between 1 and {SceneTiming.MaxSceneDuration.TotalSeconds:0}."
                    });

                sceneDuration = TimeSpan.FromSeconds(durationSeconds);
            }

            if (!controlService.EnqueueMessage(request.Text, sceneDuration, out var error))
                return Results.BadRequest(new { error });

            return Results.Ok(new { queued = "message" });
        });

        app.MapPost("/api/mode", (SetModeRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Mode))
                return Results.BadRequest(new { error = "Mode is required." });

            var normalized = request.Mode.Trim().ToLowerInvariant();
            bool testMode;
            switch (normalized)
            {
                case "normal":
                    testMode = false;
                    break;
                case "test":
                    testMode = true;
                    break;
                default:
                    return Results.BadRequest(new { error = "Mode must be 'normal' or 'test'." });
            }

            var changed = controlService.SetMode(testMode);
            return Results.Ok(new { mode = normalized, changed });
        });

        app.MapPost("/api/queue/clear", () =>
        {
            controlService.ClearQueue();
            return Results.Ok(new { cleared = true });
        });

        app.MapGet("/api/frame", () =>
        {
            var png = framePresenter.CapturePresentedFramePng(sceneRenderer);
            return Results.Bytes(png, "image/png");
        });

        app.MapGet("/health", () => Results.Ok(new { ok = true }));

        app.MapFallback(() => Results.NotFound(new { error = "Not found." }));

        Console.WriteLine(
            $"Control web UI listening on http://{options.BindAddress}:{options.Port} (token {(string.IsNullOrWhiteSpace(options.Token) ? "disabled" : "enabled")}).");
        if (!hasWebUi)
            Console.WriteLine($"Warning: control UI file not found at '{indexPath}'. API is still available.");
        if (!string.IsNullOrWhiteSpace(options.Token))
            Console.WriteLine($"Use header X-Advent-Token to call API endpoints: {contextlessApiHint(options.Port)}");

        return app;
    }

    private static string contextlessApiHint(int port)
    {
        return $"curl -H 'X-Advent-Token: <token>' http://<pi-ip>:{port}/api/status";
    }

    private sealed record PlaySceneRequest(string Name);
    private sealed record ShowMessageRequest(string Text, double? DurationSeconds);
    private sealed record SetModeRequest(string Mode);
}

internal sealed record WebControlOptions(bool Enabled, string BindAddress, int Port, string? Token)
{
    private const string DefaultBindAddress = "0.0.0.0";
    private const int DefaultPort = 8080;

    public static WebControlOptions FromEnvironment()
    {
        var enabled = ReadBool("ADVENT_WEB_ENABLED", true);
        var bindAddress = ReadString("ADVENT_WEB_BIND", DefaultBindAddress);
        var port = ReadPort("ADVENT_WEB_PORT", DefaultPort);
        var token = ReadString("ADVENT_WEB_TOKEN", string.Empty);
        if (string.IsNullOrWhiteSpace(token))
            token = null;

        return new WebControlOptions(enabled, bindAddress, port, token);
    }

    private static bool ReadBool(string envName, bool defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (bool.TryParse(raw, out var parsedBool))
            return parsedBool;

        return raw.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }

    private static string ReadString(string envName, string defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        return string.IsNullOrWhiteSpace(raw) ? defaultValue : raw.Trim();
    }

    private static int ReadPort(string envName, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (!int.TryParse(raw, out var port) || port is < 1 or > 65535)
            return defaultValue;

        return port;
    }
}
