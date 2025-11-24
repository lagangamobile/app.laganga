using com.laganga.app.Shared.Models;
using Duende.IdentityModel.OidcClient;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Services;

public interface IApiClient
{
    Task<Result<T>> GetAsync<T>(string url, CancellationToken ct = default);
    Task<Result<T>> PostAsync<T>(string url, object body, CancellationToken ct = default);
    Task<Result<T>> PutAsync<T>(string url, object body, CancellationToken ct = default);
    Task<Result<T>> DeleteAsync<T>(string url, CancellationToken ct = default);
    HttpClient getClient();
    string? GetBaseAddress();
}

// Para manejar multiple Origenes
public interface IApiGangaClient : IApiClient { }
public interface IApiGangaOauth : IApiClient { }

public class ApiClient : IApiClient, IApiGangaClient, IApiGangaOauth
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient client)
    {
        _client = client;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    // ===============================
    // POST
    // ===============================
    public async Task<Result<T>> PostAsync<T>(string url, object body, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        return await SendAsync<Result<T>>(request, ct);
    }

    // ===============================
    // GET
    // ===============================
    public async Task<Result<T>> GetAsync<T>(string url, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync<Result<T>>(request, ct);
    }

    // ===============================
    // PUT
    // ===============================
    public async Task<Result<T>> PutAsync<T>(string url, object body, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };

        return await SendAsync<Result<T>>(request, ct);
    }

    // ===============================
    // DELETE
    // ===============================
    public async Task<Result<T>> DeleteAsync<T>(string url, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        return await SendAsync<Result<T>>(request, ct);
    }

    // ===============================
    // MÉTODO INTERNO GENÉRICO
    // ===============================
    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _client.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        try
        {
            // Intentamos deserializar a Response<T> (o al tipo T si lo pides directamente)
            //var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
            //await response.Content.ReadFromJsonAsync<Result<OrigenMovimiento>>(cancellationToken: ct);
            var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            if (result != null)
                return result;
        }
        catch (JsonException)
        {
            // Si el contenido no es JSON válido, generamos una respuesta uniforme
        }

        // Si hay error o el JSON no es válido, devolvemos Response de error
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var innerType = typeof(T).GetGenericArguments()[0];
            var errorResponseType = typeof(Result<>).MakeGenericType(innerType);

            var errorInstance = Activator.CreateInstance(errorResponseType);
            var succeededProp = errorResponseType.GetProperty("Succeeded");
            var messageProp = errorResponseType.GetProperty("Message");
            var errorsProp = errorResponseType.GetProperty("Errors");

            succeededProp?.SetValue(errorInstance, false);
            messageProp?.SetValue(errorInstance, $"Error HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            errorsProp?.SetValue(errorInstance, new List<string> { content });

            return (T)errorInstance!;
        }

        // Si no es un Response<T>, devolvemos por defecto
        return Activator.CreateInstance<T>();
    }
    public HttpClient getClient()
    {
        return _client;
    }
    public string? GetBaseAddress()
    {
        return _client.BaseAddress?.ToString();
    }
}
