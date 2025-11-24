using System.Net;
using System.Net.Http.Headers;

namespace com.laganga.app.Shared.Services;

public class TokenAuthorizationMessageHandler : DelegatingHandler
{
    private readonly IAuthenticationService _auth;

    public TokenAuthorizationMessageHandler(IAuthenticationService auth)
    {
        _auth = auth;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _auth.GetValidAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            try { await _auth.LogoutAsync(); } catch { /* no-op */ }
        }

        return response;
    }
}