using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using ScaleStreamer.Common.Onvif;
using ScaleStreamer.Common.Settings;
using ScaleStreamer.Service;
using SharpOnvifServer;
using Serilog;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Xml;

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
        if (settings.RtspStream.RequireAuth)
        {
            builder.Services.AddOnvifDigestAuthentication();
        }
        else
        {
            // Register a no-op authentication scheme that always succeeds.
            // This satisfies the ASP.NET auth middleware requirement from
            // UseOnvif() while allowing NVRs to connect without credentials.
            builder.Services.AddAuthentication("NoOp")
                .AddScheme<AuthenticationSchemeOptions, NoOpAuthHandler>("NoOp", null);
            // Also register the "Digest" scheme as no-op since UseOnvif() references it
            builder.Services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, NoOpAuthHandler>("Digest", null);
        }

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
        // Axis VAPIX CGI compatibility — Dahua NVRs probe these endpoints
        // after detecting an AXIS manufacturer in ONVIF device info.
        // Must be registered before UseOnvif() to intercept before the SOAP pipeline.
        app.Use(async (context, next) =>
        {
            if (context.Request.Method == "GET" &&
                context.Request.Path.Value != null &&
                context.Request.Path.Value.StartsWith("/axis-cgi/"))
            {
                var action = context.Request.Query["action"].ToString();
                var group = context.Request.Query["group"].ToString().ToLowerInvariant();
                Log.Information("Axis CGI: action={Action} group={Group}", action, group);

                context.Response.ContentType = "text/plain";
                if (action == "list")
                {
                    if (group.StartsWith("root.brand"))
                    {
                        await context.Response.WriteAsync(
                            "root.Brand.Brand=AXIS\r\n" +
                            "root.Brand.ProdFullName=AXIS M1065-LW Network Camera\r\n" +
                            "root.Brand.ProdNbr=M1065-LW\r\n" +
                            "root.Brand.ProdShortName=AXIS M1065-LW\r\n" +
                            "root.Brand.ProdType=Network Camera\r\n" +
                            "root.Brand.ProdVariant=\r\n" +
                            "root.Brand.WebURL=http://www.axis.com\r\n");
                        return;
                    }
                    if (group.StartsWith("root.properties"))
                    {
                        await context.Response.WriteAsync(
                            "root.Properties.API.HTTP.Version=3\r\n" +
                            "root.Properties.API.Metadata.Metadata=yes\r\n" +
                            "root.Properties.API.RTSP.Version=2.01\r\n" +
                            "root.Properties.EmbeddedDevelopment.Version=2.16\r\n" +
                            "root.Properties.Firmware.BuildDate=Dec 2023\r\n" +
                            "root.Properties.Firmware.BuildNumber=1\r\n" +
                            "root.Properties.Firmware.Version=9.80.3.1\r\n" +
                            "root.Properties.Image.Format=jpeg,mjpeg,h264\r\n" +
                            "root.Properties.Image.NbrOfViews=2\r\n" +
                            $"root.Properties.Image.Resolution={settings.RtspStream.VideoWidth}x{settings.RtspStream.VideoHeight}\r\n" +
                            "root.Properties.Image.Rotation=0,180\r\n" +
                            "root.Properties.System.SerialNumber=" + settings.ScaleConnection.ScaleId + "\r\n");
                        return;
                    }
                    if (group.StartsWith("root.streamprofile"))
                    {
                        await context.Response.WriteAsync(
                            "root.StreamProfile.MaxGroups=26\r\n" +
                            "root.StreamProfile.S0.Description=profile_1_h264\r\n" +
                            "root.StreamProfile.S0.Name=profile_1_h264\r\n" +
                            "root.StreamProfile.S0.Parameters=videocodec=h264\r\n");
                        return;
                    }
                }
                await context.Response.WriteAsync("OK");
                return;
            }

            // Extract SOAP action from request body when not in Content-Type header.
            // Dahua NVRs (and some other ONVIF clients) don't include the action parameter
            // in the Content-Type header, causing CoreWCF to return ActionNotSupported (500).
            if (context.Request.Method == "POST" &&
                context.Request.ContentType?.Contains("soap+xml") == true)
            {
                context.Request.EnableBuffering();
                try
                {
                    using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(body);

                    var nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
                    nsMgr.AddNamespace("s", "http://www.w3.org/2003/05/soap-envelope");
                    var bodyChild = xmlDoc.SelectSingleNode("//s:Body/*[1]", nsMgr);
                    if (bodyChild != null)
                    {
                        var action = $"{bodyChild.NamespaceURI}/{bodyChild.LocalName}";
                        Log.Information("ONVIF SOAP: {Path} -> {Action}", context.Request.Path, action);

                        if (!context.Request.ContentType.Contains("action="))
                        {
                            context.Request.ContentType += $"; action=\"{action}\"";
                        }
                    }
                }
                catch
                {
                    context.Request.Body.Position = 0;
                }
            }
            await next();
        });

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

/// <summary>
/// Authentication handler that always succeeds — used when ONVIF auth is disabled
/// </summary>
public class NoOpAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public NoOpAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity("NoOp");
        identity.AddClaim(new Claim(ClaimTypes.Name, "anonymous"));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
