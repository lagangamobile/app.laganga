using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;
using Microsoft.Maui.Storage;

namespace com.laganga.app.Shared.Services;

public interface IAuthenticationService
{
    // Propiedades
    bool IsAuthenticated { get; }
    bool IsInitialized { get; }
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
    Task EnsureClaimsLoadedAsync();
    Task WaitForInitializationAsync();
}

public class AuthenticationService : IAuthenticationService
{
    public event Action? AuthenticationStateChanged;

    private readonly IApiGangaOauth _apiGangaOauth;
    private DiscoveryDocumentResponse? _discovery;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);
    private readonly SemaphoreSlim _claimsSemaphore = new(1, 1);
    private CancellationTokenSource? _expirationWatcherCts;
    
    // TaskCompletionSource para esperar la inicialización
    private readonly TaskCompletionSource<bool> _initializationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken) && CurrentUser.Identity?.IsAuthenticated == true;
    public bool IsInitialized { get; private set; }
    public ClaimsPrincipal CurrentUser { get; private set; } = new(new ClaimsIdentity());
    public string? AccessToken { get; private set; }
    public string? IdToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset AccessTokenExpiration { get; private set; } = DateTimeOffset.MinValue;

    private string? _lastUser;
    private string? _lastPassword;
    private bool _claimsLoaded;

    private const string AccessTokenKey = "auth.access_token";
    private const string RefreshTokenKey = "auth.refresh_token";
    private const string IdTokenKey = "auth.id_token";
    private const string ExpirationKey = "auth.access_token_exp";
    private const string UserNameKey = "auth.username";
    private const string ClaimsKey = "auth.claims";

    // Margen de 5 minutos antes de expiración para refrescar
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);
    
    // Máximo de reintentos de refresco antes de cerrar sesión
    private const int MaxRefreshRetries = 3;
    private int _refreshRetryCount;

    // Flag para evitar refrescos concurrentes
    private bool _isRefreshing;

    public AuthenticationService(IApiGangaOauth apiGangaOauth)
    {
        _apiGangaOauth = apiGangaOauth;
        _ = InitializeAsync();
    }

    /// <summary>
    /// Espera a que el servicio de autenticación termine de inicializarse.
    /// </summary>
    public Task WaitForInitializationAsync() => _initializationTcs.Task;

    private async Task InitializeAsync()
    {
        try
        {
            await TryRestoreSessionAsync();
        }
        finally
        {
            IsInitialized = true;
            _initializationTcs.TrySetResult(true);
            System.Diagnostics.Debug.WriteLine($"[AuthService] Inicialización completada. IsAuthenticated: {IsAuthenticated}");
        }
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
                throw new Exception($"Login failed: {tokenResponse.Json?.GetProperty("detail").GetString()}");

            MapTokenResponse(tokenResponse);
            _refreshRetryCount = 0;
            
            System.Diagnostics.Debug.WriteLine($"[AuthService] Login exitoso. Expiración: {AccessTokenExpiration:O}");
            
            await BuildClaimsPrincipalAsync();
            await PersistSessionAsync();

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
        _isRefreshing = false;
        _claimsLoaded = false;
        _refreshRetryCount = 0;

        await ClearPersistedAsync();
        NotifyAuthenticationStateChanged();
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        await _tokenSemaphore.WaitAsync();
        try
        {
            if (string.IsNullOrEmpty(AccessToken))
                return null;

            var now = DateTimeOffset.UtcNow;

            // Si el token ya expiró completamente
            if (now >= AccessTokenExpiration)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] Token expirado. Intentando refrescar...");
                
                if (!string.IsNullOrEmpty(RefreshToken))
                {
                    var refreshed = await RefreshAccessTokenInternalAsync();
                    if (refreshed)
                    {
                        _refreshRetryCount = 0;
                        return AccessToken;
                    }
                    
                    // Si falló pero aún tenemos reintentos, no cerrar sesión inmediatamente
                    if (_refreshRetryCount < MaxRefreshRetries)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AuthService] Refresco falló, reintento {_refreshRetryCount}/{MaxRefreshRetries}");
                        return null; // Retornar null pero no cerrar sesión aún
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[AuthService] No se pudo refrescar token, cerrando sesión");
                await LogoutAsync();
                return null;
            }

            // Si el token necesita refresco (está próximo a expirar)
            if (NeedsRefresh() && !string.IsNullOrEmpty(RefreshToken))
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] Token próximo a expirar, refrescando proactivamente...");
                var refreshed = await RefreshAccessTokenInternalAsync();
                if (refreshed)
                {
                    _refreshRetryCount = 0;
                }
                // Si falla el refresco proactivo, el token actual aún es válido
            }

            return AccessToken;
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    /// <summary>
    /// Asegura que las claims estén cargadas. Útil para componentes que necesitan claims completos.
    /// </summary>
    public async Task EnsureClaimsLoadedAsync()
    {
        if (_claimsLoaded || string.IsNullOrEmpty(AccessToken))
            return;

        await _claimsSemaphore.WaitAsync();
        try
        {
            if (_claimsLoaded)
                return;

            // Verificar si ya tenemos claims válidos
            var existingClaims = CurrentUser?.Claims?.ToList() ?? [];
            if (existingClaims.Any(c => c.Type == "given_name" || c.Type == "name" || c.Type == "nickname"))
            {
                _claimsLoaded = true;
                return;
            }

            // Intentar cargar claims desde el UserInfo endpoint
            await BuildClaimsPrincipalAsync();
            
            if (CurrentUser?.Claims?.Any(c => c.Type == "given_name" || c.Type == "name") == true)
            {
                await PersistClaimsAsync();
                _claimsLoaded = true;
                NotifyAuthenticationStateChanged();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Error cargando claims: {ex.Message}");
        }
        finally
        {
            _claimsSemaphore.Release();
        }
    }

    /// <summary>
    /// Método interno para refrescar el token sin usar el semáforo principal (evita deadlock).
    /// </summary>
    private async Task<bool> RefreshAccessTokenInternalAsync()
    {
        if (_isRefreshing)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Ya hay un refresco en progreso");
            return false;
        }

        if (string.IsNullOrEmpty(RefreshToken))
            return false;

        _isRefreshing = true;
        _refreshRetryCount++;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Iniciando refresco de token. Intento: {_refreshRetryCount}");

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
                System.Diagnostics.Debug.WriteLine($"[AuthService] Error al refrescar token: {refreshResponse.Error} - {refreshResponse.ErrorDescription}");
                
                // Si el refresh token es inválido o expiró, limpiar
                if (refreshResponse.Error == "invalid_grant")
                {
                    _refreshRetryCount = MaxRefreshRetries; // Forzar logout
                }
                return false;
            }

            var oldAccessToken = AccessToken;
            MapTokenResponse(refreshResponse);
            
            System.Diagnostics.Debug.WriteLine($"[AuthService] Token refrescado exitosamente. Nueva expiración: {AccessTokenExpiration:O}");

            // Reconstruir claims si el token cambió
            if (oldAccessToken != AccessToken)
            {
                await BuildClaimsPrincipalAsync();
            }

            await PersistSessionAsync();

            // Reiniciar el watcher con la nueva expiración
            StartExpirationWatcher();
            NotifyAuthenticationStateChanged();

            return true;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Error de red al refrescar: {ex.Message}");
            // Sin red: no incrementar retry count excesivamente, podría ser temporal
            _refreshRetryCount = Math.Max(0, _refreshRetryCount - 1);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Timeout al refrescar: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Excepción inesperada al refrescar: {ex}");
            return false;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private bool NeedsRefresh()
    {
        if (string.IsNullOrEmpty(AccessToken)) return false;
        if (AccessTokenExpiration == DateTimeOffset.MinValue) return true;

        var refreshThreshold = AccessTokenExpiration - RefreshSkew;
        return DateTimeOffset.UtcNow >= refreshThreshold;
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

        if (response.ExpiresIn > 0)
            AccessTokenExpiration = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
        else
            AccessTokenExpiration = DateTimeOffset.UtcNow.AddMinutes(5);
    }

    private async Task BuildClaimsPrincipalAsync()
    {
        var claims = new List<Claim>();

        // Intentar obtener claims del UserInfo endpoint
        if (_discovery?.UserInfoEndpoint != null && !string.IsNullOrEmpty(AccessToken))
        {
            try
            {
                await EnsureDiscoveryAsync();
                
                var userInfo = await _apiGangaOauth.getClient().GetUserInfoAsync(new UserInfoRequest
                {
                    Address = _discovery.UserInfoEndpoint,
                    Token = AccessToken
                });

                if (!userInfo.IsError && userInfo.Claims != null)
                {
                    claims.AddRange(userInfo.Claims.Select(c => new Claim(c.Type, c.Value)));
                    System.Diagnostics.Debug.WriteLine($"[AuthService] Claims obtenidos del UserInfo: {string.Join(", ", claims.Select(c => c.Type))}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AuthService] Error obteniendo UserInfo: {userInfo.Error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] Excepción obteniendo UserInfo: {ex.Message}");
            }
        }

        // Agregar claim de usuario si no existe
        if (!string.IsNullOrEmpty(_lastUser) && !claims.Any(c => c.Type == ClaimTypes.Name))
        {
            claims.Add(new Claim(ClaimTypes.Name, _lastUser));
        }

        // Asegurar que haya al menos un claim de autenticación
        if (!claims.Any())
        {
            claims.Add(new Claim("auth", "password_grant"));
        }

        var identity = new ClaimsIdentity(claims, "password");
        CurrentUser = new ClaimsPrincipal(identity);
        _claimsLoaded = claims.Any(c => c.Type == "given_name" || c.Type == "name" || c.Type == "nickname");
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

            System.Diagnostics.Debug.WriteLine($"[AuthService] Restaurando sesión. Expiración: {AccessTokenExpiration:O}, Ahora: {DateTimeOffset.UtcNow:O}");

            // Restaurar claims persistidos
            var claims = await RestoreClaimsAsync();

            // Si no hay claims persistidos o el token expiró, intentar obtenerlos del servidor
            var now = DateTimeOffset.UtcNow;
            if (now >= AccessTokenExpiration)
            {
                // Token expirado, intentar refrescar
                if (!string.IsNullOrEmpty(RefreshToken))
                {
                    System.Diagnostics.Debug.WriteLine($"[AuthService] Token expirado al restaurar, intentando refrescar...");
                    var refreshed = await RefreshAccessTokenInternalAsync();
                    if (!refreshed)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AuthService] No se pudo refrescar al restaurar, limpiando sesión");
                        await ClearPersistedAsync();
                        AccessToken = null;
                        return;
                    }
                    // BuildClaimsPrincipalAsync ya fue llamado en RefreshAccessTokenInternalAsync
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AuthService] Token expirado sin refresh token, limpiando sesión");
                    await ClearPersistedAsync();
                    AccessToken = null;
                    return;
                }
            }
            else if (!claims.Any(c => c.Type == "given_name" || c.Type == "name" || c.Type == "nickname"))
            {
                // Token válido pero sin claims completos, crearlos con lo que tenemos
                if (!string.IsNullOrEmpty(_lastUser))
                    claims.Add(new Claim(ClaimTypes.Name, _lastUser));
                
                claims.Add(new Claim("restored", "true"));
                
                var identity = new ClaimsIdentity(claims, "restored");
                CurrentUser = new ClaimsPrincipal(identity);
                
                // Cargar claims completos en background
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500); // Pequeña espera para que la app se inicialice
                    await EnsureClaimsLoadedAsync();
                });
            }
            else
            {
                // Claims restaurados correctamente
                var identity = new ClaimsIdentity(claims, "restored");
                CurrentUser = new ClaimsPrincipal(identity);
                _claimsLoaded = true;
            }

            System.Diagnostics.Debug.WriteLine($"[AuthService] Sesión restaurada. RefreshToken presente: {!string.IsNullOrEmpty(RefreshToken)}, Claims: {CurrentUser.Claims.Count()}");

            StartExpirationWatcher();
            NotifyAuthenticationStateChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Error restaurando sesión: {ex.Message}");
        }
    }

    private async Task<List<Claim>> RestoreClaimsAsync()
    {
        var claims = new List<Claim>();
        
        try
        {
            var claimsJson = await SecureStorage.GetAsync(ClaimsKey);
            if (!string.IsNullOrEmpty(claimsJson))
            {
                var claimsDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(claimsJson);
                if (claimsDictionary != null)
                {
                    claims.AddRange(claimsDictionary.Select(kvp => new Claim(kvp.Key, kvp.Value)));
                    System.Diagnostics.Debug.WriteLine($"[AuthService] Claims restaurados: {string.Join(", ", claims.Select(c => c.Type))}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Error restaurando claims: {ex.Message}");
        }

        return claims;
    }

    private async Task PersistSessionAsync()
    {
        await PersistTokensAsync();
        await PersistClaimsAsync();
    }

    private async Task PersistTokensAsync()
    {
        try
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

            if (!string.IsNullOrEmpty(_lastUser))
                await SecureStorage.SetAsync(UserNameKey, _lastUser);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Error persistiendo tokens: {ex.Message}");
        }
    }

    private async Task PersistClaimsAsync()
    {
        try
        {
            var claims = CurrentUser?.Claims?.ToList() ?? [];
            if (claims.Any())
            {
                // Convertir claims a diccionario para serializar
                // Nota: Si hay claims duplicados, tomamos el primero
                var claimsDictionary = new Dictionary<string, string>();
                foreach (var claim in claims)
                {
                    if (!claimsDictionary.ContainsKey(claim.Type))
                    {
                        claimsDictionary[claim.Type] = claim.Value;
                    }
                }

                var claimsJson = JsonSerializer.Serialize(claimsDictionary);
                await SecureStorage.SetAsync(ClaimsKey, claimsJson);
                System.Diagnostics.Debug.WriteLine($"[AuthService] Claims persistidos: {claims.Count}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Error persistiendo claims: {ex.Message}");
        }
    }

    private async Task ClearPersistedAsync()
    {
        try
        {
            SecureStorage.Remove(AccessTokenKey);
            SecureStorage.Remove(RefreshTokenKey);
            SecureStorage.Remove(IdTokenKey);
            SecureStorage.Remove(ExpirationKey);
            SecureStorage.Remove(UserNameKey);
            SecureStorage.Remove(ClaimsKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] Error limpiando storage: {ex.Message}");
        }
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
                    var refreshTime = AccessTokenExpiration - RefreshSkew;

                    // Calcular cuánto esperar antes del próximo intento de refresco
                    var delay = refreshTime - now;

                    if (delay > TimeSpan.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ExpirationWatcher] Esperando {delay.TotalMinutes:F1} minutos hasta el próximo refresco");
                        
                        // Esperar en intervalos más pequeños para poder cancelar más rápido
                        var maxWait = TimeSpan.FromMinutes(1);
                        while (delay > TimeSpan.Zero && !ct.IsCancellationRequested)
                        {
                            var waitTime = delay > maxWait ? maxWait : delay;
                            await Task.Delay(waitTime, ct);
                            delay -= waitTime;
                        }
                    }

                    if (ct.IsCancellationRequested) break;

                    // Intentar refrescar el token directamente (sin usar GetValidAccessTokenAsync para evitar deadlock)
                    if (NeedsRefresh() && !string.IsNullOrEmpty(RefreshToken))
                    {
                        await _tokenSemaphore.WaitAsync(ct);
                        try
                        {
                            var success = await RefreshAccessTokenInternalAsync();
                            if (!success && _refreshRetryCount >= MaxRefreshRetries)
                            {
                                System.Diagnostics.Debug.WriteLine("[ExpirationWatcher] Máximo de reintentos alcanzado, cerrando sesión");
                                await LogoutAsync();
                                break;
                            }
                        }
                        finally
                        {
                            _tokenSemaphore.Release();
                        }
                    }

                    // Pequeña pausa para evitar loops muy rápidos
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[ExpirationWatcher] Watcher cancelado");
            }
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