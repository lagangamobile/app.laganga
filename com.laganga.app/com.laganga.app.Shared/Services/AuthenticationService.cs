using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;
using Microsoft.Maui.Storage;

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
    Task LoginAsync(string user, string password);
    Task LogoutAsync();
    Task<string?> GetValidAccessTokenAsync();
}

public class AuthenticationService : IAuthenticationService
{
    public event Action? AuthenticationStateChanged;

    private readonly IApiGangaOauth _apiGangaOauth;
    private DiscoveryDocumentResponse? _discovery;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);
    private CancellationTokenSource? _expirationWatcherCts;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken) && CurrentUser.Identity?.IsAuthenticated == true;
    public ClaimsPrincipal CurrentUser { get; private set; } = new(new ClaimsIdentity());
    public string? AccessToken { get; private set; }
    public string? IdToken { get; private set; } // Normalmente no provisto en password grant
    public string? RefreshToken { get; private set; }
    public DateTimeOffset AccessTokenExpiration { get; private set; } = DateTimeOffset.MinValue;

    // Últimas credenciales (solo memoria; NO persistir password)
    private string? _lastUser;
    private string? _lastPassword;

    // SecureStorage keys
    private const string AccessTokenKey = "auth.access_token";
    private const string RefreshTokenKey = "auth.refresh_token";
    private const string IdTokenKey = "auth.id_token";
    private const string ExpirationKey = "auth.access_token_exp";
    private const string UserNameKey = "auth.username"; // Opcional (no password)

    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

    public AuthenticationService(IApiGangaOauth apiGangaOauth)
    {
        _apiGangaOauth = apiGangaOauth;
        _ = TryRestoreSessionAsync();
    }


    public async Task LoginAsync(string user, string password)
    {
        try
        {
            _lastUser = user;
            _lastPassword = password;

            await EnsureDiscoveryAsync();

            var tokenResponse = await _apiGangaOauth.getClient().RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = _discovery!.TokenEndpoint,
                ClientId = "71cf1cb3-1803-49d3-b26b-d81baa6296be",
                ClientSecret = "N2NmYzJlZTAtY2E3Mi00Y2I4LTg3YjItY2E0Y2EwMDAwMDAw",
                UserName = user,
                Password = password,
                Scope = "api.laganga.read api.laganga.write offline_access openid profile"
            });

            if (tokenResponse.IsError)
                throw new Exception($"Login failed: {tokenResponse.Error}");

            MapTokenResponse(tokenResponse);
            await BuildClaimsPrincipalAsync();
            await PersistTokensAsync(user);

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
        StopExpirationWatcher();

        AccessToken = null;
        IdToken = null;
        RefreshToken = null;
        AccessTokenExpiration = DateTimeOffset.MinValue;
        CurrentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _lastPassword = null;

        await ClearPersistedAsync();
        NotifyAuthenticationStateChanged();
        await Task.CompletedTask;
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        await _tokenSemaphore.WaitAsync();
        try
        {
            if (string.IsNullOrEmpty(AccessToken))
                return null;

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
                    await EnsureDiscoveryAsync();
                    var refreshResponse = await _apiGangaOauth.getClient().RequestRefreshTokenAsync(new RefreshTokenRequest
                    {
                        Address = _discovery!.TokenEndpoint,
                        ClientId = "71cf1cb3-1803-49d3-b26b-d81baa6296be",
                        ClientSecret = "N2NmYzJlZTAtY2E3Mi00Y2I4LTg3YjItY2E0Y2EwMDAwMDAw",
                        RefreshToken = RefreshToken
                    });

                    if (refreshResponse.IsError)
                    {
                        // Revocado / inválido
                        await LogoutAsync();
                        return null;
                    }

                    MapTokenResponse(refreshResponse);
                    await PersistTokensAsync(_lastUser);
                    StartExpirationWatcher();
                }
                catch (HttpRequestException)
                {
                    // Sin red: permitir uso si no expiró totalmente
                    if (DateTimeOffset.UtcNow >= AccessTokenExpiration)
                    {
                        await LogoutAsync();
                        return null;
                    }
                }
            }
            else if (DateTimeOffset.UtcNow >= AccessTokenExpiration)
            {
                await LogoutAsync();
                return null;
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
        return DateTimeOffset.UtcNow >= AccessTokenExpiration - RefreshSkew;
    }

    private async Task EnsureDiscoveryAsync()
    {
        if (_discovery != null && !_discovery.IsError) return;
        _discovery = await _apiGangaOauth.getClient().GetDiscoveryDocumentAsync(_apiGangaOauth.GetBaseAddress());
        if (_discovery.IsError)
            throw new Exception($"Discovery error: {_discovery.Error}");
    }

    private void MapTokenResponse(TokenResponse response)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken ?? RefreshToken;
        IdToken = response.IdentityToken ?? IdToken;
        // ExpiresIn (segundos)
        if (response.ExpiresIn > 0)
            AccessTokenExpiration = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
        else
            AccessTokenExpiration = DateTimeOffset.UtcNow.AddMinutes(5); // fallback defensivo

    }

    private async Task BuildClaimsPrincipalAsync()
    {
        var claims = new List<Claim>();

        // UserInfo si existe endpoint y scope openid/profile
        if (_discovery?.UserInfoEndpoint != null && !string.IsNullOrEmpty(AccessToken))
        {
            try
            {
                var userInfo = await _apiGangaOauth.getClient().GetUserInfoAsync(new UserInfoRequest
                {
                    Address = _discovery.UserInfoEndpoint,
                    Token = AccessToken
                });

                if (!userInfo.IsError)
                {
                    claims.AddRange(userInfo.Claims.Select(c => new Claim(c.Type, c.Value)));
                }
            }
            catch
            {
                // Silencioso, continuamos con claims mínimos
            }
        }

        // Claims mínimos
        if (!string.IsNullOrEmpty(_lastUser))
            claims.Add(new Claim(ClaimTypes.Name, _lastUser));

        if (!claims.Any())
            claims.Add(new Claim("auth", "password_grant"));

        var identity = new ClaimsIdentity(claims, "password");
        CurrentUser = new ClaimsPrincipal(identity);
    }

    private async Task TryRestoreSessionAsync()
    {
        try
        {
            var access = await SecureStorage.GetAsync(AccessTokenKey);
            if (string.IsNullOrEmpty(access)) return;

            AccessToken = access;
            RefreshToken = await SecureStorage.GetAsync(RefreshTokenKey);
            IdToken = await SecureStorage.GetAsync(IdTokenKey);
            _lastUser = await SecureStorage.GetAsync(UserNameKey);

            var expString = await SecureStorage.GetAsync(ExpirationKey);
            if (long.TryParse(expString, out var ticks))
                AccessTokenExpiration = new DateTimeOffset(ticks, TimeSpan.Zero);

            // Reclama identidad mínima
            var claims = new List<Claim>
            {
                new("restored","true")
            };
            if (!string.IsNullOrEmpty(_lastUser))
                claims.Add(new(ClaimTypes.Name, _lastUser));

            CurrentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "restored"));

            StartExpirationWatcher();
            NotifyAuthenticationStateChanged();
        }
        catch
        {
            // Ignorar restauración fallida
        }
    }

    private async Task PersistTokensAsync(string? userName)
    {
        if (!string.IsNullOrEmpty(AccessToken))
            await SecureStorage.SetAsync(AccessTokenKey, AccessToken);
        else
            SecureStorage.Remove(AccessTokenKey);

        if (!string.IsNullOrEmpty(RefreshToken))
            await SecureStorage.SetAsync(RefreshTokenKey, RefreshToken);
        else
            SecureStorage.Remove(RefreshTokenKey);

        if (!string.IsNullOrEmpty(IdToken))
            await SecureStorage.SetAsync(IdTokenKey, IdToken);
        else
            SecureStorage.Remove(IdTokenKey);

        await SecureStorage.SetAsync(ExpirationKey, AccessTokenExpiration.UtcTicks.ToString());

        if (!string.IsNullOrEmpty(userName))
            await SecureStorage.SetAsync(UserNameKey, userName);
    }

    private async Task ClearPersistedAsync()
    {
        SecureStorage.Remove(AccessTokenKey);
        SecureStorage.Remove(RefreshTokenKey);
        SecureStorage.Remove(IdTokenKey);
        SecureStorage.Remove(ExpirationKey);
        SecureStorage.Remove(UserNameKey);
        await Task.CompletedTask;
    }

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
                    if (nextCheck < now) nextCheck = now;

                    var delay = nextCheck - now;
                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay, ct);

                    if (ct.IsCancellationRequested) break;

                    var token = await GetValidAccessTokenAsync();
                    if (string.IsNullOrEmpty(token) || !IsAuthenticated)
                        break;

                    if (DateTimeOffset.UtcNow >= AccessTokenExpiration)
                    {
                        await LogoutAsync();
                        break;
                    }
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

    private void NotifyAuthenticationStateChanged() => AuthenticationStateChanged?.Invoke();
}