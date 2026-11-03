using ShaunWhiteReborn.Services;
using System.Security.Authentication;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

string pfxPath = Path.Combine(Directory.GetCurrentDirectory(), "shaunwhite.pfx");

builder.WebHost.ConfigureKestrel(serverOptions =>
{
#pragma warning disable SYSLIB0039
    serverOptions.ListenAnyIP(80);
    serverOptions.ListenAnyIP(3074);
    serverOptions.ListenAnyIP(3036);
    serverOptions.ListenAnyIP(3078);
    
    serverOptions.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps(pfxPath, "password123", httpsOptions =>
        {
            httpsOptions.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
        });
    });

    serverOptions.ListenAnyIP(8443, listenOptions =>
    {
        listenOptions.UseHttps(pfxPath, "password123", httpsOptions =>
        {
            httpsOptions.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
        });
    });
#pragma warning restore SYSLIB0039
});

builder.Services.AddHostedService<DNSProxyService>();

var app = builder.Build();

// AGRESSZÍV LOGGING MIDDLEWARE
app.Use(async (context, next) =>
{
    var req = context.Request;
    Console.WriteLine("\n=========================================");
    Console.WriteLine($"[DEBUG REQ] {req.Method} {req.Scheme}://{req.Host}{req.Path}{req.QueryString}");
    
    Console.WriteLine("[DEBUG HEADERS]");
    foreach (var header in req.Headers)
    {
        Console.WriteLine($"  {header.Key}: {header.Value}");
    }

    req.EnableBuffering();
    if (req.ContentLength > 0)
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Console.WriteLine($"[DEBUG BODY]\n{body}");
        req.Body.Position = 0;
    }
    Console.WriteLine("=========================================\n");

    await next();
    
    Console.WriteLine($"[DEBUG RES] Status: {context.Response.StatusCode} for {req.Path}");
});

app.MapGet("/", () => "Shaun White Reborn API is running! Made with love by Adam Stark.");

app.MapMethods("/{**path}", new[] { "GET", "POST", "PUT" }, async (HttpContext context, string path) => 
{
    string host = context.Request.Host.Host.ToLower();
    
    if (host.Contains("ubisoft.com") || host.Contains("ubi.com"))
    {
        string requestPath = "/" + path.ToLower();

        // 1. Ha a játék az XML konfigurációt kéri (bármelyik ismert végponton)
        if (requestPath.Contains("configservice.svc") || requestPath.Contains("matchmakingconfig.aspx"))
        {
            Console.WriteLine($"[MATCH] Config XML elküldve a következőnek: {requestPath}");
            
            string cleanXmlResponse = """
            <RESPONSE xmlns="">
                <AuthenticationServer><VALUE>lb-lsg-auth.ubisoft.com:3078</VALUE></AuthenticationServer>
                <AuthPS3BaseURL><VALUE>http://ps3webauth.ubi.com/EnterExclusiveContent.aspx</VALUE></AuthPS3BaseURL>
                <GatorAuthServiceActivity><VALUE>UGC_FARCRY2_PS3</VALUE></GatorAuthServiceActivity>
                <GatorAuthServiceHost><VALUE>gatorservice.ubi.com</VALUE></GatorAuthServiceHost>
                <GatorAuthServicePort><VALUE>80</VALUE></GatorAuthServicePort>
                <GatorAuthServiceURI><VALUE>https://secure.ubi.com/gatorservice/AuthenticationServiceSSL.svc</VALUE></GatorAuthServiceURI>
                <GatorConfigServiceURI><VALUE>https://secure.ubi.com/gatorservice/ConfigService.svc</VALUE></GatorConfigServiceURI>
                <GatorContentServiceURI><VALUE>https://secure.ubi.com/gatorservice/ContentService.svc</VALUE></GatorContentServiceURI>
                <GatorServiceHost><VALUE>gatorservice.ubi.com</VALUE></GatorServiceHost>
                <GatorServicePort><VALUE>80</VALUE></GatorServicePort>
                <GatorTransferServiceURI><VALUE>/TransferService.svc</VALUE></GatorTransferServiceURI>
                <GatorUserServiceURI><VALUE>https://secure.ubi.com/gatorservice/GatorUserService.svc</VALUE></GatorUserServiceURI>
                <LobbyServer><VALUE>lb-lsg-prod.ubisoft.com:3036</VALUE></LobbyServer>
                <MmpTitleId><VALUE>0x2786</VALUE></MmpTitleId>
                <NetworkPlatformCommunicationId><VALUE>NPWR00419</VALUE></NetworkPlatformCommunicationId>
                <NetworkPlatformServiceId><VALUE>UP0001-BLUS30178_00</VALUE></NetworkPlatformServiceId>
                <SubContextName><VALUE>FARCRY2_PS3</VALUE></SubContextName>
                <UmProductID><VALUE>PL-FC_F-FC3_SF-FC3PS3</VALUE></UmProductID>
                <UmSonyServiceId><VALUE>UP0001-BLUS30178_00</VALUE></UmSonyServiceId>
                <UmSTSServerName><VALUE>uts.ubi.com</VALUE></UmSTSServerName>
                <UmSTSServerPort><VALUE>80</VALUE></UmSTSServerPort>
                <UmTypeID><VALUE>GAME</VALUE></UmTypeID>
                <UmUTSServerName><VALUE>uts.ubi.com</VALUE></UmUTSServerName>
                <UmUTSServerPort><VALUE>80</VALUE></UmUTSServerPort>
            </RESPONSE>
            """;

            return Results.Text(cleanXmlResponse, "application/xml");
        }
        
        // 2. Minden más Ubisoft (pl. UTS tracking) kérésre adjunk vissza egy sima, üres sikeres státuszt
        Console.WriteLine($"[MATCH] Üres 200 OK elküldve a következőnek: {requestPath}");
        return Results.Ok();
    }

    Console.WriteLine($"[UNHANDLED HOST] Ismeretlen host: {host}");
    return Results.NotFound();
});

app.Run();