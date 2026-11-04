using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using ShaunWhiteReborn.Services;
using ShaunWhiteReborn.Services.Demonware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel.Https", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel.BadRequests", LogLevel.Information);

string pfxPath = Path.Combine(Directory.GetCurrentDirectory(), "shaunwhite.pfx");

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = null;

    serverOptions.ListenAnyIP(80);
    serverOptions.ListenAnyIP(3074);

    foreach (int port in new[] { 443, 8443 })
    {
        serverOptions.ListenAnyIP(port, listenOptions =>
        {
            listenOptions.UseHttps(pfxPath, "password123", httpsOptions =>
            {
                httpsOptions.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
            });
        });
    }
});

builder.Services.AddHostedService<DNSProxyService>();
builder.Services.AddHostedService<DemonwareServer>();

var app = builder.Build();

const string ConfigXml = """
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

byte[] configBytes = Encoding.UTF8.GetBytes(Regex.Replace(ConfigXml, @">\s+<", "><").Trim());

app.Run(async context =>
{
    var request = context.Request;
    var response = context.Response;

    response.Headers.Connection = "close";

    long bodyBytes = 0;
    byte[] buffer = new byte[65536];
    int read;
    while ((read = await request.Body.ReadAsync(buffer)) > 0)
    {
        bodyBytes += read;
    }

    string host = request.Host.Host.ToLowerInvariant();
    string path = request.Path.Value ?? "/";
    string soapAction = request.Headers["SOAPAction"].ToString().Trim('"');
    string action = soapAction.Contains('/') ? soapAction[(soapAction.LastIndexOf('/') + 1)..] : soapAction;

    if (action.Length == 0 && path.EndsWith("TransferService.svc", StringComparison.OrdinalIgnoreCase))
    {
        action = "SaveContent";
    }

    if (HttpMethods.IsGet(request.Method) && path == "/")
    {
        response.ContentType = "text/plain; charset=utf-8";
        await response.WriteAsync("Shaun White Reborn API is running! Made with love by Adam Stark.");
        return;
    }

    if (action.Length > 0 || path.EndsWith(".svc", StringComparison.OrdinalIgnoreCase))
    {
        string label = action.Length > 0 ? action : path;
        if (!GatorSoap.IsKnown(action))
        {
            Console.WriteLine($"[GATOR] Unknown request '{label}' on {host}{path}, sending a generic reply");
        }
        else if (action == "SaveContent")
        {
            Console.WriteLine($"[GATOR] SaveContent: received {bodyBytes} bytes of upload data");
        }
        else
        {
            Console.WriteLine($"[GATOR] {label}");
        }

        byte[] soapBytes = Encoding.UTF8.GetBytes(GatorSoap.Respond(action));
        response.ContentType = "text/xml; charset=utf-8";
        response.ContentLength = soapBytes.Length;
        await response.Body.WriteAsync(soapBytes);
        return;
    }

    if (path.EndsWith("MatchMakingConfig.aspx", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"[HTTP] Game config sent to {host}");
        response.ContentType = "text/html";
        response.ContentLength = configBytes.Length;
        await response.Body.WriteAsync(configBytes);
        return;
    }

    if (host.Contains("ubisoft.com") || host.Contains("ubi.com"))
    {
        if (!path.EndsWith("session.ashx", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[HTTP] Unhandled request: {request.Method} {host}{path}");
        }
        response.ContentLength = 0;
        return;
    }

    Console.WriteLine($"[HTTP] Unknown host: {host}");
    response.StatusCode = StatusCodes.Status404NotFound;
});

app.Run();
