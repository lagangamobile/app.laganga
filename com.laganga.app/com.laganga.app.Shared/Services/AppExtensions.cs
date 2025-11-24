using Blazored.SessionStorage;
using Microsoft.Extensions.DependencyInjection;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System.Security.Authentication;

namespace com.laganga.app.Shared.Services;

public static partial class AppExtensions
{

    public static IServiceCollection AddApplicationShared(this IServiceCollection services)
    {
        services.AddDevExpressBlazor(options =>
        {
            options.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5;
            options.SizeMode = DevExpress.Blazor.SizeMode.Medium;
            
        });


        services.AddTransient<TokenAuthorizationMessageHandler>();
        services.AddTransient<ResilientApiHandler>();

        string authDomain = "https://auth.laganga.com";
        string apiBaseUrl = "https://api.laganga.com";
#if DEBUG
        authDomain = "https://auth-stg.laganga.com";
        apiBaseUrl = "https://api-stg.laganga.com/";

#endif

        // services.AddHttpClient<IApiGangaClient, ApiClient>(GetConfigApi(apiBaseUrl))
        // .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        // {
        //     SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        //     {
        //         EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
        //         RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
        //     }
        // })
        //.AddHttpMessageHandler<TokenAuthorizationMessageHandler>()
        //.AddHttpMessageHandler<ResilientApiHandler>()
        //.AddPolicyHandler(GetRetryPolicy())
        //.AddPolicyHandler(GetCircuitBreakerPolicy());


        // services.AddHttpClient<IApiGangaOauth, ApiClient>(GetConfigApi(apiBaseUrl))
        // .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        // {
        //    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        //    {
        //        EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
        //        RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
        //    }
        // })
        // .AddHttpMessageHandler<TokenAuthorizationMessageHandler>()
        // .AddHttpMessageHandler<ResilientApiHandler>()
        // .AddPolicyHandler(GetRetryPolicy())
        // .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<IApiGangaOauth, ApiClient>(GetConfigApi(authDomain))
             .ConfigurePrimaryHttpMessageHandler(CreateSocketsHandler)
             .AddHttpMessageHandler<ResilientApiHandler>()
             .AddPolicyHandler(GetRetryPolicy())
             .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<IApiGangaClient, ApiClient>(GetConfigApi(apiBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(CreateSocketsHandler)
            .AddHttpMessageHandler<TokenAuthorizationMessageHandler>()
            .AddHttpMessageHandler<ResilientApiHandler>()
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        
        services.AddScoped<LayoutService>();
        services.AddSingleton<ParameterService>();
        services.AddSingleton<NetworkStatusService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();

        services.AddBlazoredSessionStorage();

        return services;
    }


    private static Action<HttpClient> GetConfigApi(string BaseUrl) =>
    c =>
    {
        c.BaseAddress = new Uri(BaseUrl);
        c.Timeout = TimeSpan.FromSeconds(40);
        c.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        c.DefaultRequestHeaders.Add("User-Agent", "com.laganga.app");
    };


    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
      HttpPolicyExtensions
          .HandleTransientHttpError()
          .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(10, TimeSpan.FromSeconds(20));

    private static Func<HttpMessageHandler> CreateSocketsHandler => () => new SocketsHttpHandler
    {
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
            RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
        }
    };

}
