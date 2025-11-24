using Polly.CircuitBreaker;


namespace com.laganga.app.Shared.Services;

public class ResilientApiHandler : DelegatingHandler
{
    private readonly NetworkStatusService _networkStatus;

    public ResilientApiHandler(NetworkStatusService networkStatus)
    {
        _networkStatus = networkStatus;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Limpiar error si la llamada vuelve a funcionar
            if (response.IsSuccessStatusCode)
                _networkStatus.ClearApiFailure();

            return response;
        }
        catch (BrokenCircuitException bce)
        {
            _networkStatus.ReportApiFailure("Servicio temporalmente indisponible (circuito abierto). Intenta más tarde.");
            throw new NetworkUnavailableException("Circuito abierto: el servicio no responde de forma estable.", bce);
        }
        catch (TaskCanceledException tce) when (!cancellationToken.IsCancellationRequested)
        {
            _networkStatus.ReportApiFailure("Tiempo de espera excedido al contactar el API.");
            throw new NetworkUnavailableException("Timeout comunicándose con el API.", tce);
        }
        catch (HttpRequestException hre)
        {
            _networkStatus.ReportApiFailure("No se pudo conectar al API (revisa tu conexión).");
            throw new NetworkUnavailableException("Fallo de red al consumir el API.", hre);
        }
    }
}