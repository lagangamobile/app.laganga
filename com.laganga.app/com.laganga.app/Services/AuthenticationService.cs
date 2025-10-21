using com.laganga.app.Shared.Services;
using Duende.IdentityModel.OidcClient;
using System.Security.Claims;
using Microsoft.Maui.Storage;
using Duende.IdentityModel.Client;

namespace com.laganga.app.Services;

public class AuthenticationService : IAuthenticationService
{
    public event Action? AuthenticationStateChanged;

    private readonly IFormFactor _formFactor;
    private OidcClient _oidcClient = default!;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken) && CurrentUser.Identity?.IsAuthenticated == true;
    public ClaimsPrincipal CurrentUser { get; private set; } = new(new ClaimsIdentity());
    public string? AccessToken { get; private set; }
    public string? IdToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset AccessTokenExpiration { get; private set; } = DateTimeOffset.MinValue;

    private const string AccessTokenKey = "auth.access_token";
    private const string RefreshTokenKey = "auth.refresh_token";
    private const string IdTokenKey = "auth.id_token";
    private const string ExpirationKey = "auth.access_token_exp";


    private CancellationTokenSource? _expirationWatcherCts;
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

    public AuthenticationService(IFormFactor formFactor)
    {
        _formFactor = formFactor;
        _ = TryRestoreSessionAsync();
    }

    public async Task LoginAsync()
    {
        try
        {
            _oidcClient = await _formFactor.GetClientOidc();
            var loginResult = await _oidcClient.LoginAsync();

            if (loginResult.IsError)
                throw new Exception("Login failed: " + loginResult.Error);

            AccessToken = loginResult.AccessToken;
            IdToken = loginResult.IdentityToken;
            RefreshToken = loginResult.RefreshToken;
            AccessTokenExpiration = loginResult.AccessTokenExpiration;
            CurrentUser = loginResult.User;

            await PersistTokensAsync();
            StartExpirationWatcher();
            NotifyAuthenticationStateChanged();
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkUnavailableException("No se pudo contactar el servicio de autenticación.", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new NetworkUnavailableException("Timeout al autenticar.", ex);
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            StopExpirationWatcher();
            _oidcClient = await _formFactor.GetClientOidc();
            if (!string.IsNullOrEmpty(IdToken))
            {
                var logoutResult = await _oidcClient.LogoutAsync();
                if (logoutResult.IsError)
                    System.Diagnostics.Debug.WriteLine($"Logout error: {logoutResult.Error}");
            }
        }
        catch
        {
        }

        finally
        {
            AccessToken = null;
            IdToken = null;
            RefreshToken = null;
            AccessTokenExpiration = DateTimeOffset.MinValue;
            CurrentUser = new ClaimsPrincipal(new ClaimsIdentity());

            await ClearPersistedAsync();
            NotifyAuthenticationStateChanged();
        }
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        await _tokenSemaphore.WaitAsync();
        try
        {
            if (NeedsRefresh())
            {
                if (string.IsNullOrEmpty(RefreshToken))
                {
                    if (DateTimeOffset.UtcNow >= AccessTokenExpiration)
                    {
                        await LogoutAsync();
                        return null;
                    }
                    return AccessToken;
                }

                try
                {
                    _oidcClient = await _formFactor.GetClientOidc();
                    var refreshResult = await _oidcClient.RefreshTokenAsync(RefreshToken);
                    if (!refreshResult.IsError)
                    {
                        AccessToken = refreshResult.AccessToken;
                        IdToken = refreshResult.IdentityToken ?? IdToken;
                        RefreshToken = refreshResult.RefreshToken ?? RefreshToken;
                        AccessTokenExpiration = refreshResult.AccessTokenExpiration;
                        await PersistTokensAsync();
                        StartExpirationWatcher();
                    }
                    else
                    {
                        await LogoutAsync();
                        return null;
                    }
                }
                catch (HttpRequestException)
                {
                    if (DateTimeOffset.UtcNow >= AccessTokenExpiration)
                    {
                        await LogoutAsync();
                        return null;
                    }
                }
            }
            else
            {
                // Si ya pasó la expiración y no se detectó antes
                if (DateTimeOffset.UtcNow >= AccessTokenExpiration && !string.IsNullOrEmpty(AccessToken))
                {
                    await LogoutAsync();
                    return null;
                }
            }
            return AccessToken;
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    private bool NeedsRefresh()
    {
        if (string.IsNullOrEmpty(AccessToken)) return false;
        
        if (AccessTokenExpiration == DateTimeOffset.MinValue) return true;

        // Refresca 60s antes de expirar
        return DateTimeOffset.UtcNow >= AccessTokenExpiration - RefreshSkew;
    }

    private async Task TryRestoreSessionAsync()
    {
        try
        {
            var access = await SecureStorage.GetAsync(AccessTokenKey);
            if (string.IsNullOrEmpty(access)) return;

            AccessToken = access;
            IdToken = await SecureStorage.GetAsync(IdTokenKey);
            RefreshToken = await SecureStorage.GetAsync(RefreshTokenKey);

            var expString = await SecureStorage.GetAsync(ExpirationKey);
            if (long.TryParse(expString, out var ticks))
                AccessTokenExpiration = new DateTimeOffset(ticks, TimeSpan.Zero);

            // Reclama identidad mínima (solo si quieres claims persistidos deberías serializarlos)
            CurrentUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("restored","true")
            }, "oidc"));

            StartExpirationWatcher();
            NotifyAuthenticationStateChanged();
        }
        catch { /* Ignorar */ }
    }

    private async Task PersistTokensAsync()
    {
        if (!string.IsNullOrEmpty(AccessToken))
            await SecureStorage.SetAsync(AccessTokenKey, AccessToken);
        if (!string.IsNullOrEmpty(IdToken))
            await SecureStorage.SetAsync(IdTokenKey, IdToken);
        if (!string.IsNullOrEmpty(RefreshToken))
            await SecureStorage.SetAsync(RefreshTokenKey, RefreshToken);

        await SecureStorage.SetAsync(ExpirationKey, AccessTokenExpiration.UtcTicks.ToString());
    }

    private async Task ClearPersistedAsync()
    {
        SecureStorage.Remove(AccessTokenKey);
        SecureStorage.Remove(IdTokenKey);
        SecureStorage.Remove(RefreshTokenKey);
        SecureStorage.Remove(ExpirationKey);
        await Task.CompletedTask;
    }

    private void NotifyAuthenticationStateChanged() => AuthenticationStateChanged?.Invoke();
    private void StartExpirationWatcher()
    {
        StopExpirationWatcher();

        if (string.IsNullOrEmpty(AccessToken) || AccessTokenExpiration == DateTimeOffset.MinValue)
            return;

        _expirationWatcherCts = new CancellationTokenSource();
        var ct = _expirationWatcherCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested && !string.IsNullOrEmpty(AccessToken))
                {
                    var now = DateTimeOffset.UtcNow;
                    var nextCheck = AccessTokenExpiration - RefreshSkew;
                    if (nextCheck < now)
                        nextCheck = now; // inmediata

                    var delay = nextCheck - now;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, ct);

                    if (ct.IsCancellationRequested) break;

                    // Forzar intento de refresco / logout
                    var token = await GetValidAccessTokenAsync();
                    if (string.IsNullOrEmpty(token) || !IsAuthenticated)
                        break;

                    // Si después del refresco ya estamos más allá de la expiración -> salir
                    if (DateTimeOffset.UtcNow >= AccessTokenExpiration)
                    {
                        await LogoutAsync();
                        break;
                    }

                    // Ciclo continúa hasta que el token se invalide o logout
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExpirationWatcher] Error: {ex}");
            }
        }, ct);
    }

    private void StopExpirationWatcher()
    {
        try
        {
            _expirationWatcherCts?.Cancel();
            _expirationWatcherCts?.Dispose();
        }
        catch { }
        finally
        {
            _expirationWatcherCts = null;
        }
    }
}