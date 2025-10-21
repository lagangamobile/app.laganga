using System.Net.Http.Headers;
using com.laganga.app.Shared.Services;

namespace com.laganga.app.Services;

public class TokenAuthorizationMessageHandler : DelegatingHandler
{
    private readonly IAuthenticationService _auth;

    public TokenAuthorizationMessageHandler(IAuthenticationService auth)
    {
        _auth = auth;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_auth is AuthenticationService concrete)
        {
            var token = await concrete.GetValidAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        return await base.SendAsync(request, cancellationToken);
    }
}