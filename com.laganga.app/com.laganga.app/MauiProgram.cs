using com.laganga.app.Services;
using com.laganga.app.Shared.Services;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System.Security.Authentication;


namespace com.laganga.app;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddApplicationShared();
        

        string authDomain = "https://auth.laganga.com";
        string apiBaseUrl = "https://api.laganga.com";

        

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();

        authDomain = "https://auth-stg.laganga.com";
        apiBaseUrl = "https://api-stg.laganga.com/";


        

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[UNHANDLED EXCEPTION] {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[UNOBSERVED TASK EXCEPTION] {e.Exception}");
        };


#endif

        // Add device-specific services used by the com.laganga.app.Shared project
        builder.Services.AddSingleton<Duende.IdentityModel.OidcClient.Browser.IBrowser, MauiAuthenticationBrowser>();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        

        // Handler's DI
        builder.Services.AddTransient<TokenAuthorizationMessageHandler>();
        builder.Services.AddTransient<ResilientApiHandler>();

        //// HttpClient nombrado (reutilizable y eficiente)
        builder.Services.AddHttpClient("Api", c =>
        {
            c.BaseAddress = new Uri(apiBaseUrl);
            c.Timeout = TimeSpan.FromSeconds(40);
            c.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
            }
        })
        .AddHttpMessageHandler<TokenAuthorizationMessageHandler>()
        .AddHttpMessageHandler<ResilientApiHandler>()
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());


        // Cliente OIDC
        builder.Services.AddSingleton(provider =>
        {
            var browser = provider.GetRequiredService<Duende.IdentityModel.OidcClient.Browser.IBrowser>();
            return new OidcClient(new OidcClientOptions
            {
                Authority = authDomain,
                ClientId = "71cf1cb3-1803-49d3-b26b-d81baa6296be",
                ClientSecret = "N2NmYzJlZTAtY2E3Mi00Y2I4LTg3YjItY2E0Y2EwMDAwMDAw",
                Scope = "api.laganga.read api.laganga.write offline_access openid profile",
                RedirectUri = "laganga://callback",
                PostLogoutRedirectUri = "laganga://callback",
                Browser = browser,
                BackchannelHandler = new SocketsHttpHandler
                {
                    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                    {
                        EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
                        RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
                    }
                }
            });
        });

        return builder.Build();
    }


    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(10, TimeSpan.FromSeconds(20));


    // Reintento con retroceso exponencial hasta 3 veces
    //private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    //{
    //    return HttpPolicyExtensions
    //        .HandleTransientHttpError()
    //        .WaitAndRetryAsync(
    //            retryCount: 3,
    //            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s
    //            onRetry: (outcome, timespan, retryAttempt, context) =>
    //            {
    //                Console.WriteLine($"Intento {retryAttempt} fallido. Reintentando en {timespan.TotalSeconds} segundos...");
    //            });
    //}

    //// Circuit Breaker: abre el circuito si hay 5 errores en 30 segundos, y lo mantiene abierto 1 minuto
    //private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    //{
    //    return HttpPolicyExtensions
    //        .HandleTransientHttpError()
    //        .CircuitBreakerAsync(
    //            handledEventsAllowedBeforeBreaking: 5,
    //            durationOfBreak: TimeSpan.FromMinutes(1),
    //            onBreak: (outcome, timespan) =>
    //            {
    //                Console.WriteLine($"Circuito abierto durante {timespan.TotalSeconds} segundos.");
    //            },
    //            onReset: () => Console.WriteLine("Circuito cerrado. Tráfico restaurado."),
    //            onHalfOpen: () => Console.WriteLine("Circuito en estado HALF-OPEN: probando la recuperación.")
    //        );
    //}
}
