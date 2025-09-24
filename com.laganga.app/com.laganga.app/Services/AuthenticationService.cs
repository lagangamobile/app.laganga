using com.laganga.app.Shared.Services;
using Duende.IdentityModel.OidcClient;
using Microsoft.Maui.ApplicationModel;
using System.Security.Claims;

namespace com.laganga.app.Services;

public class AuthenticationService : IAuthenticationService
{
    public event Action? AuthenticationStateChanged;

    private readonly IFormFactor _formFactor;
    private OidcClient _oidcClient;

    public AuthenticationService(IFormFactor formFactor)
    {
        _formFactor = formFactor;
    }

    public bool IsAuthenticated => CurrentUser?.Identity?.IsAuthenticated ?? false;
    public ClaimsPrincipal CurrentUser { get; private set; } = new ClaimsPrincipal(new ClaimsIdentity());
    public string? AccessToken { get; private set; }
    public string? IdToken { get; private set; }

    public async Task LoginAsync()
    {
        // 1. Obtiene el OidcClient
        _oidcClient = await _formFactor.GetClientOidc();

        // 2. Realiza el proceso de login
        var loginResult = await _oidcClient.LoginAsync();

        if (!loginResult.IsError)
        {
            // 3. Almacena los tokens y las claims
            AccessToken = loginResult.AccessToken;
            IdToken = loginResult.IdentityToken;
            CurrentUser = loginResult.User;
            NotifyAuthenticationStateChanged();
        }
        else
        {
            // Opcional: Manejo de errores
            Console.WriteLine($"Login error: {loginResult.Error}");
            throw new Exception("Login failed: " + loginResult.Error);
        }

    }

    public async Task LogoutAsync()
    {
        AccessToken = null;
        IdToken = null;
        CurrentUser = new ClaimsPrincipal(new ClaimsIdentity());


        _oidcClient = await _formFactor.GetClientOidc();

        //if (_oidcClient != null && !string.IsNullOrEmpty(IdToken))
        //{
        //    var logoutResult = await _oidcClient.LogoutAsync(new LogoutRequest
        //    {
        //        IdTokenHint = IdToken
        //    });

        //}

        if (_oidcClient != null)
        {
            var logoutRequest = new LogoutRequest();
            var logoutResult = await _oidcClient.LogoutAsync(logoutRequest);
            if (logoutResult.IsError)
            {
                Console.WriteLine($"Logout error: {logoutResult.Error}");
            }
        }
        NotifyAuthenticationStateChanged();
    }

    private void NotifyAuthenticationStateChanged() => AuthenticationStateChanged?.Invoke();
}