using com.laganga.app.Services;
using com.laganga.app.Shared.Services;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.Logging;
using System.Security.Authentication;

namespace com.laganga.app
{
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

            string authDomain = "https://auth.laganga.com";

            var handler = new SocketsHttpHandler
            {
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    // Habilita el protocolo TLS 1.3.
                    EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,

                }
            };

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();

            authDomain = "https://auth-stg.laganga.com";

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UNHANDLED EXCEPTION] {e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UNOBSERVED TASK EXCEPTION] {e.Exception}");
            };

            handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                // Permitir todos los certificados (solo para desarrollo)
                return true;
            };
#endif

            // Add device-specific services used by the com.laganga.app.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            //builder.Services.AddSingleton<Duende.IdentityModel.OidcClient.Browser.IBrowser, MauiAuthenticationBrowser>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

            builder.Services.AddSingleton(provider =>
            {
                //var browser = provider.GetRequiredService<Duende.IdentityModel.OidcClient.Browser.IBrowser>();

                return new OidcClient(new OidcClientOptions
                {
                    Authority = authDomain,
                    ClientId = "71cf1cb3-1803-49d3-b26b-d81baa6296be",
                    ClientSecret = "N2NmYzJlZTAtY2E3Mi00Y2I4LTg3YjItY2E0Y2EwMDAwMDAw",
                    Scope = "openid profile api.laganga.read api.laganga.write",
                    RedirectUri = "laganga://callback",
                    PostLogoutRedirectUri = "laganga://callback",
                    Browser = new MauiAuthenticationBrowser(),
                    //Browser = browser,
                    BackchannelHandler = handler,
                });
            });

            return builder.Build();
        }
    }
}
