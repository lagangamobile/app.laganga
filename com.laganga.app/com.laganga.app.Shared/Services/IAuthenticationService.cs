using System.Security.Claims;

namespace com.laganga.app.Shared.Services;

public interface IAuthenticationService
{
    // Propiedades
    bool IsAuthenticated { get; }
    ClaimsPrincipal CurrentUser { get; }
    string? AccessToken { get; }
    string? IdToken { get; }
    string? RefreshToken { get; }
    DateTimeOffset AccessTokenExpiration { get; }

    // Eventos
    event Action? AuthenticationStateChanged;

    // Métodos
    Task LoginAsync();
    Task LogoutAsync();
    Task<string?> GetValidAccessTokenAsync();
}