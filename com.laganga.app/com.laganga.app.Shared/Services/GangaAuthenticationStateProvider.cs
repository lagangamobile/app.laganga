using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace com.laganga.app.Shared.Services;

public sealed class GangaAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IAuthenticationService _authService;

    public GangaAuthenticationStateProvider(IAuthenticationService authService)
    {
        _authService = authService;
        _authService.AuthenticationStateChanged += OnAuthStateChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = _authService.IsAuthenticated
            ? _authService.CurrentUser
            : new ClaimsPrincipal(new ClaimsIdentity());

        return Task.FromResult(new AuthenticationState(principal));
    }

    private void OnAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void Dispose()
    {
        _authService.AuthenticationStateChanged -= OnAuthStateChanged;
    }
}
