using System.Security.Claims;

namespace com.laganga.app.Shared.Services;

public interface IAuthenticationService
{
    event Action? AuthenticationStateChanged;

    Task LoginAsync();
    Task LogoutAsync();

    bool IsAuthenticated { get; }
    ClaimsPrincipal CurrentUser { get; }
    string? AccessToken { get; }
    string? IdToken { get; }
}