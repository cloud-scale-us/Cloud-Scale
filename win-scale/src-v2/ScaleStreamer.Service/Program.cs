using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ScaleStreamer.Common.Onvif;
using ScaleStreamer.Common.Settings;
using ScaleStreamer.Service;
using SharpOnvifServer;
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

    var settings = AppSettings.Instance;

    var builder = WebApplication.CreateBuilder(args);

    // Configure Kestrel to listen on ONVIF HTTP port
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(settings.Onvif.HttpPort);
    });

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

    // ONVIF services
    if (settings.Onvif.Enabled)
    {
        builder.Services.AddServiceModelServices();
        builder.Services.AddServiceModelMetadata();
        builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

        // ONVIF WS-Security digest auth
        builder.Services.AddSingleton<IUserRepository, OnvifUserRepository>();
        builder.Services.AddOnvifDigestAuthentication();

        if (settings.Onvif.DiscoveryEnabled)
        {
            builder.Services.AddOnvifDiscovery();
        }

        builder.Services.AddSingleton<ScaleDeviceService>();
        builder.Services.AddSingleton<ScaleMediaService>();
    }

    var app = builder.Build();

    if (settings.Onvif.Enabled)
    {
        app.UseOnvif();

        ((IApplicationBuilder)app).UseServiceModel(serviceBuilder =>
        {
            var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
            serviceMetadataBehavior.HttpGetEnabled = true;

            serviceBuilder.AddService<ScaleDeviceService>();
            serviceBuilder.AddServiceEndpoint<ScaleDeviceService, SharpOnvifServer.DeviceMgmt.Device>(
                OnvifBindingFactory.CreateBinding(), "/onvif/device_service");

            serviceBuilder.AddService<ScaleMediaService>();
            serviceBuilder.AddServiceEndpoint<ScaleMediaService, SharpOnvifServer.Media.Media>(
                OnvifBindingFactory.CreateBinding(), "/onvif/media_service");
        });

        Log.Information("ONVIF Profile S enabled on port {Port}, discovery={Discovery}",
            settings.Onvif.HttpPort, settings.Onvif.DiscoveryEnabled);
    }

    Log.Information("Scale Streamer Service starting host...");
    await app.RunAsync();

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
