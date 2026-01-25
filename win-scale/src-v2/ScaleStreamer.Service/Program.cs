using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScaleStreamer.Service;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ScaleStreamer", "logs", "service-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Scale Streamer Service starting...");

    var builder = Host.CreateApplicationBuilder(args);

    // Configure service to run as Windows Service
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "ScaleStreamerService";
    });

    // Add Serilog
    builder.Services.AddSerilog();

    // Register application services
    builder.Services.AddSingleton<ScaleConnectionManager>();
    builder.Services.AddHostedService<ScaleService>();

    var host = builder.Build();

    Log.Information("Scale Streamer Service starting host...");
    await host.RunAsync();

    Log.Information("Scale Streamer Service stopped.");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Scale Streamer Service terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
